// SegDebLogger.cpp : Defines the entry point for the console application.
//
// Author: Mikael af Enehielm, Landis+Gyr Oy
//
// Console coloring part is based on http://www.cpluplus.com/articles/Eyhv0pDG/
//   (Article written by eklavya sharma 2, Mar 7 2013)

//
// TODO and further enhancements:
//  - improve the task info reading and stack probing. Seems to work a bit better when in embos source define these:
//      #define OS_COM_IN_BUFFER_SIZE (250u)
//      #define OS_COM_OUT_BUFFER_SIZE (250u)
//
// - Decouple serial port handling and console handling into separate threads.
//   The idea would be that when user does a mark&copy operation with mouse in console window, we prevent that the serial port handling is stalled.
//   Currently when marking in console, the console writing gets blocked and thus the whole process gets blocked. This can be prevented!
//
// - with option enable background stack polling with timestamps
// - with option enable cpu load polling with timestamps (similar as the embosView graph, but into file with infinite history).
//   Having that we could perhaps see if cpu load was cause of watchdog or other problem. i.e. other than staring at embosView graph.
//
// - Add a timer that is re-triggered at start of received package (state LDState_SD0), expect the whole received data packet to come in decent time.
//   If timer expires before we get the end of packet (state LDState_EOD), reset state-machine to idle-state. 
//   This may sometimes garbage leftovers from old packets to be seen, but rather that than
//   getting the protocol locked up. What is "decent time"? a second? half a second? 
//
#include "stdafx.h"
#include <string.h>
#include <share.h>

static FILE *timlogfp = NULL; // TODO: remove this

bool useRawDump = false;

#define MAX_EVENT_COUNT 3
int eventCount = 0;                                     // Number of events to wait for
HANDLE hCom = INVALID_HANDLE_VALUE;                     // handle for COM port communication
HANDLE hAbortEvent = INVALID_HANDLE_VALUE;              // Event that abort requests can be signaled on
HANDLE hStackpollEvent = INVALID_HANDLE_VALUE;          // Event that triggers a stack poll request
HANDLE ghEventList[MAX_EVENT_COUNT];
char *eventName = "Global\\SegDebLogger";
char *eventNameStackPoll = "Global\\SegDebLogger_StackPoll";

OVERLAPPED  m_OverlappedRead  = {0};
OVERLAPPED  m_OverlappedWrite = {0};
DWORD rxErrors      = 0;                                // overrun, parity and framing errors
int   rxIndex       = 0;                                // next location to store in rxBuffer
int   rxReadPos     = 0;                                // next position to read in rxBuffer
unsigned char       rxBuffer[4096];                      // received data from Embedded System
FILE *outFile = NULL;
bool doConsoleOutput = true;
bool doAutoNewline = true;
bool doTimeStamps = false;
bool doAppendToFile = false;
bool doTaskAndStackPolling = true;
int doVerboseMessages = 0;
int gotData = 0;
bool abortProcess = false;  // Set by Ctrl-C handler.
bool signalAnAbort = false;
bool waitForAbort  = false;
char *taskName = NULL;

int ReadData( void *buffer, int limit );
int WriteCommand_GetTaskList();
int WriteCommand_GetTaskInfo_ByIndex(int taskIndex);
int WriteCommand_GetStackCheck(unsigned int stackPtr, unsigned int checkLength);
void logDebugData( unsigned char *logDataBuffer, int bytesInBuffer);
void logDataOPGenerateOutput( );
void logDebugDataAddChar( unsigned char dc );
void logOffProtocolData(unsigned char dc );
int WriteCommand(const unsigned char *transmitBuffer, int bufferLength);
void ServiceHandler_ListTasks( );
void ServiceHandler_TaskInfo( );
void ServiceHandler_StackProbe( );


static int debugTimeStampStuff = 0; // 1 == generate data, 2 == readback data, 0 = don't use

//---------------------------------------------------------------------------------------------------------
//
//    Class CommsSettings -- maintains the parameters for communication line.
//
class CommsSettings
{
public:
	CommsSettings(void);
	~CommsSettings(void);

	void SetComPortName(char *name);
	bool SetComPortSpeed(char *name);
	
	char *compPortName;
	int compPortSpeed;
};

CommsSettings::CommsSettings(void)
{
	// Defaults:
	compPortName = _strdup("COM10");
	compPortSpeed = 115200;
}

CommsSettings::~CommsSettings(void)
{
}


void CommsSettings::SetComPortName(char *name)
{
	if (compPortName != NULL) {
		free(compPortName);
	}

	compPortName = _strdup(name);
}


bool CommsSettings::SetComPortSpeed(char *name)
{
	if (sscanf_s(name, "%d", &compPortSpeed) != 1) {
		return false;
	}
	return true;
}

//---------------------------------------------------------------------------------------------------------
//
//  Ctrl-C handler. Flag that we want out, let the main-loop do the abort.
//
BOOL WINAPI CtrlC_HandlerRoutine(/*__in*/  DWORD dwCtrlType)
{
    if (dwCtrlType == CTRL_C_EVENT ) {
        printf("Got Ctrl-C !!\n");
        abortProcess  = true;
        return true; // Let it continue still. The flag will cause an abort.
    }
    return false; // Exception not handled
}

//---------------------------------------------------------------------------------------------------------
// Console coloring
//
class ConsoleOutput
{
public:
   ConsoleOutput() {}
   virtual void print(char *buffer, int bufflen);
};
void ConsoleOutput::print(char *buffer, int bufflen)
{
   fwrite(buffer, bufflen, 1, stdout);
}
class ConsoleColoring: public ConsoleOutput
{
public:
   enum concol {
      black=0,
      dark_blue=1,
      dark_green = 2,
      dark_cyan = 3,
      dark_red = 4,
      dark_purple = 5,
      dark_yellow = 6,
      dark_white = 7,
      gray = 8,
      blue = 9,
      green = 10,
      cyan = 11,
      red = 12,
      purple = 13,
      yellow = 14,
      white = 15
   };

private:
   HANDLE std_con_out;
   void update_colors();
   concol textCol, backCol, defTextCol, defBackCol;

public:

   ConsoleColoring();
   // virtual void print(char *buffer, int bufflen);
   virtual void setColor(concol newTextColor, concol newBackColor);
   virtual void setDefColors( );

};
ConsoleColoring::ConsoleColoring( )
{
   std_con_out = GetStdHandle( STD_OUTPUT_HANDLE);
   update_colors();
   defTextCol = textCol;
   defBackCol = backCol;
}
void ConsoleColoring::setDefColors( )
{
   setColor(defTextCol, defBackCol);
}

void ConsoleColoring::setColor(concol newTextColor, concol newBackColor)
{
   // SetConsoleTextAttribute(buffer, bufflen, 1, stdout);
   textCol = newTextColor;
   backCol = newBackColor;
   unsigned short wAttributes = ((unsigned int)backCol << 4) | (unsigned int)textCol;
   SetConsoleTextAttribute(std_con_out, wAttributes);
}
void ConsoleColoring::update_colors()
{
   CONSOLE_SCREEN_BUFFER_INFO csbi;
   GetConsoleScreenBufferInfo(std_con_out, &csbi);
   textCol = concol(csbi.wAttributes & 15);
   backCol = concol((csbi.wAttributes & 0xf0) >> 4);
}

ConsoleColoring *co;

//--------------------------------------------------------------------------------------------------------
void printFileError(int _errno, char *fName)
{
   char buffer[200];
   strerror_s(buffer, sizeof(buffer), _errno);
   fprintf(stderr, "\n%s, %s\n", buffer, fName);
}

//---------------------------------------------------------------------------------------------------------
//
//                --------------------[  Main ]--------------
//
int _tmain(int argc, _TCHAR* argv[])
{
	int i;
	bool Continue;
   bool FirstRequest = true;
	int RetVal = 0;
	int requestNumber = 0;
   CommsSettings commsSettings;
   co = new ConsoleColoring( );

    // Trap Ctrl-C so we get a clean exit.
    SetConsoleCtrlHandler(CtrlC_HandlerRoutine, true );

    /* TODO: remove this debug stuff when working reliably.
    if (timlogfp == NULL) {
       timlogfp = fopen("C:/temp/timlog.txt", "wt");
    }
    */


   // Process all the files passed on the command line
   i = 1;
   Continue = true;
   int opterr = 0; // Counts number of errors on command-line
   while ((i < argc) && Continue)
   {
      if (strncmp(argv[i], "-h", 2) == 0 ||
	            strncmp(argv[i], "--help", 2) == 0 ||
	            strncmp(argv[i], "-?", 2) == 0 ||
               strncmp(argv[i], "/?", 2) == 0 ) { // Dos-style
         // just need help ?
         opterr++;
         Continue = false;

      } else if (strncmp(argv[i], "-v", 2) == 0) {
         doVerboseMessages++;
         i++;

      } else if (strncmp(argv[i], "-c", 2) == 0) {
         commsSettings.SetComPortName( argv[i]+2 );
         i++;

      } else if (strncmp(argv[i], "-p", 2) == 0) {
         doTaskAndStackPolling = true;

         if (strlen(argv[i]) > 2) {
            eventNameStackPoll = argv[i] + 2;
         }
         i++;

      } else if (strncmp(argv[i], "-r", 2) == 0) {
         useRawDump = true; // Not implemented yet
         i++;

      } else if (strncmp(argv[i], "-q", 2) == 0) {
         doConsoleOutput = false; 
         i++;

      } else if (strncmp(argv[i], "-t", 2) == 0) {
         doTimeStamps = true; 
         i++;

      } else if (strncmp(argv[i], "-n", 2) == 0) {
         doAutoNewline = false; // Automatically add newline at end of output.
         // This affects what happens at end of an OS_SendString()
         i++;

      } else if ((strncmp(argv[i], "-f", 2) == 0) || ((doAppendToFile = (strncmp(argv[i], "-F", 2) == 0)) == true) ) {
         char *fileName = argv[i]+2;
         char *openMode = doAppendToFile ? "at" : "wt";
         
         // if ( fopen_s(&outFile, fileName, openMode) != 0) {

         // Allow some other application to view the file while we have it open for writing
         if ( (outFile = _fsopen(fileName, openMode, _SH_DENYWR)) == NULL) {
            fprintf(stderr, "Could open file %s for write\n", fileName);
            opterr++;
            Continue = false;
         }
         i++;

      } else if (strncmp(argv[i], "-s", 2) == 0) {
         if ( ! commsSettings.SetComPortSpeed( argv[i]+2 ) ) {
            fprintf(stderr, "Invalid parameter to option -s\n");
            opterr++;
            Continue = false;
         }
         i++;

      } else if ((signalAnAbort = (strncmp(argv[i], "-a", 2) == 0)) ||
                 (waitForAbort  = (strncmp(argv[i], "-w", 2) == 0))) {
         if (strlen(argv[i]) > 2) {
            eventName = argv[i] + 2;
         }

         hAbortEvent = CreateEvent( 
            NULL,               // default security attributes
            TRUE,               // manual-reset event
            FALSE,              // initial state is non-signaled
            eventName           // object name
         );

         if (hAbortEvent == NULL) { 
            fprintf(stderr, "CreateEvent %s failed (%d)\n", eventName, GetLastError());
            return 3;
         }

         // If option was to signal an abort, that will be the only task for us to do.
         // Signal the abort and exit. (i.e. no serial port logging is done, just cause
         // an other instance of this application to terminate through this event)
         if (signalAnAbort) {
            if (! SetEvent(hAbortEvent) ) 
            {
               fprintf(stderr, "SetEvent failed (%d)\n", GetLastError());
               return 4;
            }
            fprintf(stderr, "Abort signal sent on %s\n", eventName);
            return 0;
         }
         i++;

      } else if (strncmp(argv[i], "-y", 2) == 0) {
         taskName  = argv[i] + 2;
         i++;

      } else if (strcmp(argv[i], "-ShowColors") == 0) {
         //---- Test color codings
         fprintf(stdout, "SegDebLogger colors:\n");
         for (i=0 ; i < 16 ; i++) {
            co->setColor((ConsoleColoring::concol)i, ConsoleColoring::black);
            fprintf(stdout, "SegDebLogger new foreground color: %d\n", i);
            co->setDefColors();
         }
         for (i=0 ; i < 16 ; i++) {
            co->setColor(ConsoleColoring::black, (ConsoleColoring::concol)i);
            fprintf(stdout, "SegDebLogger new background color: %d\n", i);
            co->setDefColors();
         }
         fprintf(stdout, "SegDebLogger default colors again\n");
         i++;

      } else {
         // Other than option
         break;
      }

   }

   // If don't know how to use, show some help
   // i.e. if error or "-h" or no arguments at all.
   if (opterr ) {
      fprintf(stderr, "SegDebLogger, a debug logger for Seggers emBOS.\n");
      fprintf(stderr, "Version 1.12 N\n");
      fprintf(stderr, "Reads from the serial line the debug output and shows on screen or writes in file.\n");
      fprintf(stderr, "\n");


      fprintf(stderr, "Usage: SegDebLogger [-cCOMPORT]|[-sSPEED]|[-fFILE]|[-h]\n");
      fprintf(stderr, " -h : show this message\n");
      // fprintf(stderr, " -r : raw output\n");
      fprintf(stderr, " -fFILE   : write to FILE as well (truncate if file exist)\n");
      fprintf(stderr, " -FFILE   : write to FILE as well (append if file exist)\n");
      fprintf(stderr, " -q       : don't log on console (meaningful only with -f option)\n");
      fprintf(stderr, " -cPORT   : specify port name to use with serial interface (%s would be used now)\n", commsSettings.compPortName);
      fprintf(stderr, " -sSPEED  : specify speed to use with serial interface (%d would be used now)\n", commsSettings.compPortSpeed);
      fprintf(stderr, " -n       : do not add newline at end of message\n");
      fprintf(stderr, " -t       : add time-stamps to the messages\n");
      fprintf(stderr, " -w[EVNAME] : also monitor an event with name EVNAME, exit if set\n");
      fprintf(stderr, "              EVNAME should be of form %s, i.e. specify global namespace and a name\n", eventName);
      fprintf(stderr, " -a[EVNAME] : signal an event with name EVNAME, then exit.\n");
      fprintf(stderr, " -p[EVNAME] : set event name for polling tasks and stacks.\n");
      fprintf(stderr, "              EVNAME can be used to trigger this (EVNAME is set to %s).\n", eventNameStackPoll);
      fprintf(stderr, " -yTASK   : highlight the task in different color on console (TASK should be in double quotes)\n");
      fprintf(stderr, "\n");
      fprintf(stderr, "To stop the application, press Ctrl-C\n\n");
      fprintf(stderr, "To trigger stack probing issue a separate command:\n");
      fprintf(stderr, "    SegDebLogger -a%s\n", eventNameStackPoll);


		//fprintf(stderr, "Exit Status:\n");
		// fprintf(stderr, "   0 - all ok.\n");
		// fprintf(stderr, "   1 - error creating communication object.\n");
		// fprintf(stderr, "   2 - error in connect request.\n");
		// fprintf(stderr, " 255 - error on command line.\n");

		if (opterr) {
			RetVal = 255;			
		}

		return RetVal;
	}

   if (debugTimeStampStuff != 2) { // When not debugging with collected data, use real com port

	   //
	   // Create a COM handle
	   //
	   CString portName = _T("\\\\.\\") + CString(commsSettings.compPortName);
	   hCom = CreateFile( portName,
		   GENERIC_READ                     | GENERIC_WRITE,
		   0,                                // comm devices must be opened w/exclusive-access
		   NULL,                             // no security attributes
		   OPEN_EXISTING,	                   // comm devices must use OPEN_EXISTING
		   FILE_FLAG_OVERLAPPED,             // use overlapped I/O
		   NULL                              // hTemplate must be NULL for comm devices
	   );

	   if (hCom == INVALID_HANDLE_VALUE) {
		   fprintf (stderr, "COM CreateFile failed with error %d.\n", GetLastError());
		   return FALSE;
	   }

      m_OverlappedRead.hEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
      if (m_OverlappedRead.hEvent == NULL) {
         fprintf (stderr, "COM CreateEvent failed with error %d.\n", GetLastError());
         return FALSE;
      }
      m_OverlappedWrite.hEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
      if (m_OverlappedWrite.hEvent == NULL) {
         fprintf (stderr, "COM CreateEvent failed with error %d.\n", GetLastError());
         return FALSE;
      }

      if (doTaskAndStackPolling ) {

         hStackpollEvent = CreateEvent( 
            NULL,               // default security attributes
            TRUE,               // manual-reset event
            FALSE,              // initial state is non-signaled
            eventNameStackPoll  // object name
            );
         if (hStackpollEvent == NULL) { 
            fprintf(stderr, "CreateEvent %s failed (%d)\n", eventNameStackPoll, GetLastError());
            return FALSE;
         }
      }


	   // Get current configuration of serial communication port.
	   DCB dcb; //device control block for serial port parameters
	   if (GetCommState(hCom,&dcb) == 0)
	   {
	      fprintf(stderr, "Get configuration port has problem.");
	      return FALSE;
	   }
	   dcb.fBinary  = 1;
	   dcb.BaudRate = commsSettings.compPortSpeed;
	   dcb.ByteSize = 8;
	   // dcb.Parity = NOPARITY;
	   // dcb.StopBits = ONESTOPBIT;
	   dcb.fDsrSensitivity = FALSE;
	   // dcb.fOutX = FALSE;
	   // dcb.fInX = FALSE;
	   dcb.fErrorChar = FALSE;
	   // dcb.fRtsControl =RTS_CONTROL_ENABLE;
	   dcb.fAbortOnError = TRUE;
	   dcb.fOutxCtsFlow = FALSE;
	   dcb.fOutxDsrFlow = FALSE;

	   SetCommState(hCom, &dcb);

	   //-----------------------------------------------------
      DWORD fdwEventMask;
      // if (!GetCommMask (hCom, &fdwEventMask)) //get current event mask
		//	   fdwEventMask = 0;

	   fdwEventMask =   0 
					   | EV_RXCHAR 
					   | EV_TXEMPTY
					   | EV_BREAK  
					   | EV_ERR    
					   | EV_RING   
					   | EV_RLSD   
					   | EV_CTS    
					   | EV_DSR      
                  ;

	   SetCommMask(hCom, fdwEventMask);
	   EscapeCommFunction(hCom,SETDTR);

      COMMTIMEOUTS commTimeouts;
      if (!GetCommTimeouts(hCom, &commTimeouts)) {
         fprintf(stderr, "Get comm timeouts port has problem.");
         return FALSE;
      }
      commTimeouts.ReadIntervalTimeout = MAXDWORD;
      commTimeouts.ReadTotalTimeoutMultiplier = 0;
      commTimeouts.ReadTotalTimeoutConstant = 200; // Waiting at most 200 ms for bytes to be received
      if (!SetCommTimeouts(hCom, &commTimeouts)) {
         fprintf(stderr, "Get comm timeouts port has problem.");
         return FALSE;
      }
   }

	//-----------------------------------------------------
   int cnt = 0;
   while(!abortProcess) { // Stop only by Ctrl-C
      int bytesRead=0;

      if (debugTimeStampStuff == 2) { // Debug logging with collected data, here create files with it.
          static int numBuff = 0;
          char fName[200];
          sprintf_s(fName, sizeof(fName), "c:\\temp\\deb\\buf%04d.data", numBuff++);
          FILE *dumpFile = NULL;
          int localErrno;
          if((localErrno = fopen_s(&dumpFile, fName, "rb")) == 0)
          {
             bytesRead = fread(rxBuffer, 1, sizeof(rxBuffer), dumpFile);
             fclose(dumpFile);
          } else {
             printFileError(localErrno, fName);
             abortProcess = 1;
             // exit(1);
          }
      } else {   
          bytesRead = ReadData( rxBuffer, sizeof(rxBuffer));
      }

      if (bytesRead != 0) {

           if (debugTimeStampStuff == 1) { // Debugging with collected data (i.e. simulate com port)
               static int numBuff = 0;
               char fName[200];
               sprintf_s(fName, sizeof(fName), "c:\\temp\\deb\\buf%04d.data", numBuff++);
               FILE *dumpFile = NULL;
               if(fopen_s(&dumpFile, fName, "wb") == 0)
               {
                  fwrite(rxBuffer, bytesRead, 1, dumpFile);
                  fclose(dumpFile);
               }
           }

            logDebugData( rxBuffer, bytesRead);
        }
    }
	//-----------------------------------------------------
   co->setDefColors();

   if (hCom != INVALID_HANDLE_VALUE) {
      CancelIo(hCom);  // Cancel any pending operations that might have timedout
      CloseHandle(hCom);
      hCom = INVALID_HANDLE_VALUE;
      if (m_OverlappedRead.hEvent != NULL) {
         CloseHandle(m_OverlappedRead.hEvent);
      }
      if (m_OverlappedWrite.hEvent != NULL) {
         CloseHandle(m_OverlappedWrite.hEvent);
      }
    }
    if (outFile != NULL) {
        fflush(outFile);
        fclose(outFile);
        outFile = NULL;
    }
    if ((hAbortEvent != INVALID_HANDLE_VALUE) && (hAbortEvent != NULL)) {
        CloseHandle(hAbortEvent);
    }
    if ((hStackpollEvent != INVALID_HANDLE_VALUE) && (hStackpollEvent != NULL)) {
        CloseHandle(hStackpollEvent);
    }
    return RetVal;
}


void PurgeRx()
{
    DWORD commErr;
    COMSTAT commStat;

	//PurgeComm(hPort, PURGE_RXABORT | PURGE_RXCLEAR);
	ClearCommError(hCom, &commErr, &commStat);
	PurgeComm(hCom, PURGE_RXCLEAR);
	rxErrors  = 0;
	rxIndex   = 0;
	rxReadPos = 0;
	memset(rxBuffer, 0, sizeof(rxBuffer));
}


int WriteCommand(const unsigned char *transmitBuffer, int bufferLength)
{
   if( hCom == INVALID_HANDLE_VALUE ) return( 0 );

   DWORD dwBytesToWrite = bufferLength;
   DWORD dwBytesWritten = 0;

   DWORD lastError = 0;
   BOOL bWriteStatus = 0;
   bool commEventSet = false;

   bWriteStatus = WriteFile( hCom, transmitBuffer, dwBytesToWrite, &dwBytesWritten, &m_OverlappedWrite  );
   lastError = GetLastError();

   if (bWriteStatus == 0) {
      if( lastError == ERROR_IO_PENDING ){
         // fprintf(stderr, "WRITE PENDING....\n");
         DWORD dwStat; 
         dwStat = WaitForSingleObject( m_OverlappedWrite.hEvent, 400 );

         if (dwStat == WAIT_OBJECT_0) {
            commEventSet = true;
         }
         if (commEventSet) {

            BOOL bSuccess;
            DWORD dwBytesSent = 0;
            bSuccess = GetOverlappedResult(
               hCom,
               &m_OverlappedWrite,
               &dwBytesSent,
               FALSE);
            // fprintf(stderr, "%d, WROTE %d bytes\n", bSuccess, dwBytesSent);
         }

      } else if (lastError == ERROR_ACCESS_DENIED ) {
         fprintf(stderr, "Access denied....\n");
      }
   }

   return 0;
}
//---------------------------------------------------------------------------------------------
//
//     Get stuff from the serial port.
//
// There is a 200ms second time-out through using overlapped I/O.
//
static int readIssuedButNotComplete = 0;
int ReadData( void *buffer, int limit )
{

   if( hCom == INVALID_HANDLE_VALUE ) return( 0 );

   DWORD lastError = 0;

   BOOL bReadStatus = 0;
   DWORD dwBytesToRead = 0, dwBytesRead;
   DWORD dwErrorFlags, dwRes = 0;
   COMSTAT ComStat;
   memset(&ComStat, 0, sizeof(ComStat));

   dwBytesToRead = limit;
   dwBytesRead = 0;
   ClearCommError( hCom, &dwErrorFlags, &ComStat );

   /*
   if( !ComStat.cbInQue ) {
      // No bytes available yet. Wait for some for a while
      return( 0 );
   } else {
      dwBytesToRead = (DWORD) ComStat.cbInQue;
      if( limit < (int) dwBytesToRead ) dwBytesToRead = (DWORD) limit;
   }
   */

   // TODO: if there is an operation still pending which timed out before. Wait some more for that
   //       instead of issuing a new one.

   bReadStatus = ReadFile( hCom, buffer, dwBytesToRead, &dwBytesRead, &m_OverlappedRead  );
   lastError = GetLastError();

   if (bReadStatus == 0) {
      if( lastError == ERROR_IO_PENDING ){
         DWORD dwStat; 
         bool commEventSet = false;

         // Note that the order of these entries is relevant!
         eventCount = 0;
         int waitObjIndex_Abort = -1;
         int waitObjIndex_StackPoll = -1;
         int waitObjIndex_CommEvent = -1;
         if (waitForAbort) {
            waitObjIndex_Abort = eventCount;
            ghEventList[eventCount++] = hAbortEvent;             // prioritize this event
         }
         if (doTaskAndStackPolling) {
            waitObjIndex_StackPoll = eventCount;
            ghEventList[eventCount++] = hStackpollEvent;         // handle stack-poll request event
         }
         waitObjIndex_CommEvent = eventCount;
         ghEventList[eventCount++] = m_OverlappedRead.hEvent; // if not abort, then check this

         dwStat  = WaitForMultipleObjects(
            eventCount,     // number of handles in array
            ghEventList,    // array of thread handles
            FALSE,          // wait until all are signaled
            400);

         if ((waitObjIndex_Abort != -1) && (dwStat == (WAIT_OBJECT_0 + waitObjIndex_Abort))) {
            abortProcess = true;  // global variable, causes break out of main loop

         } else if ((waitObjIndex_StackPoll != -1) && (dwStat == (WAIT_OBJECT_0+waitObjIndex_StackPoll))) {

            if (! ResetEvent(hStackpollEvent) ) 
            {
               fprintf(stderr, "ResetEvent failed (%d)\n", GetLastError());
            }

            // Write request for task list. (when received a state-machine will continue reading further stuff)
            WriteCommand_GetTaskList();

         } else if (dwStat == (WAIT_OBJECT_0 + waitObjIndex_CommEvent)) {
            commEventSet = true;
         }


         if (commEventSet) {

            BOOL bSuccess;
            DWORD dwBytesGot = 0;
            bSuccess = GetOverlappedResult(
               hCom,
               &m_OverlappedRead,
               &dwBytesGot,
               FALSE);
            readIssuedButNotComplete = 0;
            if (bSuccess) {
               // This is ok, we got the data
               dwRes = dwBytesGot;
            } else {
               // An error occurred
               lastError = GetLastError();
               if (lastError == ERROR_OPERATION_ABORTED) {
                  // The I/O operation was aborted for some reason.
               }
               dwRes = 0;
            }

            if (timlogfp != NULL) {
               fprintf(timlogfp, "succ=%d, read=%d, got=%d, inqueue=%d\n", bSuccess, dwBytesRead, dwBytesGot, ComStat.cbInQue);
               fflush(timlogfp);
            }

         } else if (dwStat == WAIT_TIMEOUT) {

            // Could e.g. flush the output buffer at this point?
            // 
            readIssuedButNotComplete = 1;
            if (timlogfp != NULL) {
               fprintf(timlogfp, "timeout, read=%d, inqueue=%d\n", dwBytesRead, ComStat.cbInQue);
               fflush(timlogfp);
            }
            // TODO: Here is left a pending IO. It should be waited for again or canceled!

         } else {
            dwRes = dwBytesRead;
         }

      } else {
         if (lastError == ERROR_OPERATION_ABORTED) {
            // The I/O operation was aborted for some reason.
         }
         if (timlogfp != NULL) {
            fprintf(timlogfp, "error from ReadFile %d\n", lastError);
            fflush(timlogfp);
         }

         // Some error
      }
   } else {
      if (timlogfp != NULL) {
         fprintf(timlogfp, "got data, read=%d\n", dwBytesRead);
         fflush(timlogfp);
      }
      dwRes = dwBytesRead;
      // Got requested data already.
      // dwBytesRead == amount of data we got
   }

   return( (int) dwRes );
}

//---------------------------------------------------------------------------------------------------
//
#define LOGDATA_LeadIn_SD0  0x8C
#define LOGDATA_LeadIn_SD1  0xED
#define LOGDATA_EOD         0x8D
#define LOGDATA_STRTYPE     0x43
#define LOGDATA_LISTTYPE    0x6C  // 'l' --> LDState_List
#define LOGDATA_LIST_TASKS  0x74  // 't' --> LDState_ListTasks, expect 32-bit pointers
#define LOGDATA_TASK_INFO   0x74  // 't' --> LDState_TaskInfo, expect a block of data
#define LOGDATA_STACK_PROBE 0x63  // 'c' --> LDState_StackProbe, expect a block of data with: 'c', U16

enum LogData_states {
    LDState_Idle,
    LDState_SD0,        // Lead-in marker 1 (0x8c)
    LDState_SD1,        // Lead-in marker 2 (0xed)
    LDState_LenByte,    
    LDState_DataType,   // data type (0x43 for OS_SendString()) Included in LenByte
    LDState_StringData,       // the actual string data (several bytes as indicated by the LenByte)
    LDState_List,       // got a "list" type, from this can follow LDState_ListTasks
    LDState_ListTasks,  // got LDState_ListTasks, from this should follow a sequence of 32-bit pointers (high byte first)
    LDState_TaskInfo,   // from this should follow a block of task info data.
    LDState_StackProbe, 
    LDState_ChkSum,
    LDState_EOD,
    LDState_PossibleLeadIn   // Found in lead-in marker 1 in unexpected place.
};

//---------------------------------------------------------------------------------------------------
//
class LogDataBuffer {
public:
   LogDataBuffer(unsigned char *_buffer, int _bufferSize)
      : buffer(_buffer),
      bufferSize(_bufferSize)
   {
   }

   unsigned char *buffer;
   int bufferSize;
   int numReceivedBytes;

   // Getters for data elements in the buffer. The byte ordering in the buffer is defined by the communication protocol.
   unsigned int   GetDataU32(int offs);               // Get an U32 from the buffer, with high bytes at lower addresses
   unsigned short GetDataU16(int offs);               // Get an U16 from the buffer, with high bytes at lower addresses
   unsigned char  GetDataU8(int offs);                // Get a byte from the buffer
   int LogDataBuffer::GetDataString(int offs, unsigned char *nameBuff, int nameMaxLen);

   void SetDataU8(int offs, unsigned char dataU8);    // Stores a byte in the buffer at given offset
   void AppendDataU8(unsigned char dataU8);           // Stores a byte in the buffer and updates numReceivedBytes
   void Clear( );
};

unsigned char LogDataBuffer::GetDataU8(int offs)
{
   unsigned char dataU8 = 0;

   if ((offs) >= bufferSize) {
      return 0; // Prevent memory violation.
   }
   dataU8 = buffer[offs];

   return dataU8;
};

unsigned short LogDataBuffer::GetDataU16(int offs)
{
   unsigned short dataU16 = 0;

   if ((offs + 1) >= bufferSize) {
      return 0; // Prevent memory violation.
   }
   dataU16 |= ((unsigned int)(buffer[offs + 0])) << 8;
   dataU16 |= ((unsigned int)(buffer[offs + 1]));

   return dataU16;
};

unsigned int LogDataBuffer::GetDataU32(int offs)
{
   unsigned int dataU32 = 0;

   if ((offs + 3) >= bufferSize) {
      return 0; // Prevent memory violation.
   }
   dataU32 |= ((unsigned int)(buffer[offs + 0])) << 24;
   dataU32 |= ((unsigned int)(buffer[offs + 1])) << 16;
   dataU32 |= ((unsigned int)(buffer[offs + 2])) <<  8;
   dataU32 |= ((unsigned int)(buffer[offs + 3]));

   return dataU32;
};

void LogDataBuffer::SetDataU8(int offs, unsigned char dataU8)
{
   if (offs >= bufferSize) return;
   buffer[offs] = dataU8;
}
void LogDataBuffer::AppendDataU8(unsigned char dataU8)
{
   if (numReceivedBytes >= bufferSize) return;
   buffer[numReceivedBytes] = dataU8;
   numReceivedBytes++;
}
void LogDataBuffer::Clear( )
{
   numReceivedBytes = 0;
}

int LogDataBuffer::GetDataString(int offs, unsigned char *stringBuff, int stringMaxLen)
{
   unsigned char dataU8;
   int stringLen;
   // get string length
   if (offs >= bufferSize) return 0;
   dataU8 = this->GetDataU8(offs);
   stringLen = dataU8;
   offs += 1;

   // get string content
   for (int i=0 ; (i < stringLen) && (i < stringMaxLen) ; i++) {
      stringBuff[i] = this->GetDataU8(offs+i);
   }

   return stringLen + 1; // (+1 for the length byte)
}
//---------------------------------------------------------------------------------------------------
//
class LogDataInfo {
public:
   LogDataInfo( );
   void MakeTimeStamp( );
   bool IsErrorFatalString(unsigned char * _outBuff, const char * subStr);
   unsigned char chksum; // Checksum byte
   LogData_states state;
   LogData_states cmdReceived;
   int pkgLen;           // expected length of payload data

   bool packetError;               // error was seen in packet parsing state-machine for current packet
   char packetMessageBuffer[4096]; // for verbose messages (e.g. error messages or packet specific message)


   int dataCount;        // Count how many characters written in output
   unsigned char outBuff[256+1];  // As length in protocol is only one byte, this buffer should be large enough

   int dataCountOP;      // Count how many characters written in "out of protocol" output
   unsigned char outBuffOP[4096];  // buffer for "out of protocol" output (should be flushed on-line-by-line base)

   static const unsigned int MaxTasks = 200u;
   int numTaskPointersReceived;
   int numTaskPointerBytesReceived;
   // TODO: change to use the LogDataBuffer type for taskPointersBuffer as well.
   unsigned char taskPointersBuffer[MaxTasks*4];  // buffer for "out of protocol" output (should be flushed on-line-by-line base)
   // unsigned char taskPointersBuffer[MaxTasks*4];  // buffer for "out of protocol" output (should be flushed on-line-by-line base)

   // For requesting stack probes, remember which task and stack-size
   unsigned char currTaskName[256];
   int currStackSize;
   int currTaskIndex;


   // Task-Lister statemachine:
   enum TaskListerStateEnum {
      TaskLister_Idle,
      TaskLister_StartListAll,
      TaskLister_ListingTasks,
      TaskLister_ListStack,
      TaskLister_ListNext,
      TaskLister_Done
   };
   TaskListerStateEnum taskListerState;
   int taskLister_listTaskNum;

public:
   // int numTaskInfoBytesReceived;
   LogDataBuffer taskInfoReceiveBuffer;
   LogDataBuffer stackInfoReceiveBuffer;
private:
   unsigned char _taskInfoReceiveBuffer[200];
   unsigned char _stackInfoReceiveBuffer[200];

public:
   bool lastIsEol;
   bool hasTimeStamp;    // True if a timestamp is logged but not yet output.
   bool isFirstOnLine;     // true at first entry
   bool showTimeStamp;   // Show a timestamp as the last output ended in eol.
   SYSTEMTIME timeStamp;
};
LogDataInfo::LogDataInfo( )
   : hasTimeStamp(false)
   , isFirstOnLine(true)
   , dataCount(0)
   , dataCountOP(0)
   , showTimeStamp(true)
   , taskInfoReceiveBuffer(_taskInfoReceiveBuffer, sizeof(_taskInfoReceiveBuffer))
   , stackInfoReceiveBuffer(_stackInfoReceiveBuffer, sizeof(_stackInfoReceiveBuffer))
   , taskListerState(TaskLister_Idle)
   , taskLister_listTaskNum(0)
{
    chksum = 0;
    state = LDState_Idle;
    lastIsEol = false;
}
void LogDataInfo::MakeTimeStamp( )
{
   GetLocalTime( &timeStamp );
   hasTimeStamp = true;
}

bool LogDataInfo::IsErrorFatalString(unsigned char * _outBuff, const char * subStr)
{
   if(strstr(reinterpret_cast<char *>(_outBuff), subStr) != NULL)
   {
      return true;
   }
   return false;
}

LogDataInfo logData;

int formatTimeStamp(char *buffer, int bufferSize)
{
   int stampLen;

   if (logData.hasTimeStamp ) {
      stampLen = _snprintf_s(buffer, bufferSize, _TRUNCATE, "%04u-%02u-%02u %02u:%02u:%02u.%03u: ",
         logData.timeStamp.wYear,
         logData.timeStamp.wMonth,
         logData.timeStamp.wDay,
         logData.timeStamp.wHour,
         logData.timeStamp.wMinute,
         logData.timeStamp.wSecond,
         logData.timeStamp.wMilliseconds);
      logData.hasTimeStamp = false;
   } else {
      stampLen = _snprintf_s(buffer, bufferSize, _TRUNCATE, "--:--:--.---: ");
   }

   return stampLen;
}

//-----------------------------------------------------------------------------------------------------------------
//
// Output the log-data we have collected so far
//
void logDataGenerateOutput( )
{
   char timeStampBuffer[200];
   int stampLen = 0;
   bool doShowTimeStamp = false;
   bool updateConsoleColors = false;
   if (logData.showTimeStamp) {
      if(doTimeStamps)
      {
         stampLen = formatTimeStamp(timeStampBuffer, sizeof(timeStampBuffer));
         doShowTimeStamp = true;
         logData.showTimeStamp = false; // enabled again after next newline is output
      }
      updateConsoleColors = true;
   }

   logDataOPGenerateOutput( );

   // Generate output on console ?
   if (doConsoleOutput) {
      // gotData is for getting feedback on how much stuff get into the buffer before getting it out
      // If the number gets very large it could be an indication of that the receive buffer fills up.
      // We could thus be facing a performance issue. (i.e. risk that we may get receive-buffer overrun)
      // If the number is same as the line length then there should not be a problem.
      // printf("%5d: ", gotData);
      // gotData = 0;
      if(updateConsoleColors)
      {
         if(logData.IsErrorFatalString(logData.outBuff, "ERROR"))
         {
            co->setColor(ConsoleColoring::red, ConsoleColoring::black);
         }
         else if(logData.IsErrorFatalString(logData.outBuff, "FATAL"))
         {
            co->setColor(ConsoleColoring::purple, ConsoleColoring::black);
         }
         else if( (taskName != NULL) && (logData.IsErrorFatalString(logData.outBuff, taskName)) )
         {
            co->setColor(ConsoleColoring::yellow, ConsoleColoring::black);
         }
         else
         {
            co->setDefColors();
         }
         updateConsoleColors = false;
      }
      if (doShowTimeStamp) {
         fwrite(timeStampBuffer, stampLen, 1, stdout);
      }

      fwrite(logData.outBuff, logData.dataCount, 1, stdout);

      /*
      // TODO: parse message line with following expression.
      msgClassMatcher = re.compile(b'^' +
         b' *(?P<MsgClass>[A-Za-z]+)  *' +
         b'(?P<TickCount>[0-9]+)  *' +
         b'(?P<TaskName>[-_A-Za-z0-9 \/]+): ' +
         b'(?P<Msg>[^\r\n]*)'
         )
         */

      if ( (! logData.lastIsEol) && doAutoNewline) {
         printf("\n");
      }
   }

   // Generate output in file ?
   if (outFile != NULL) {        
      if (doShowTimeStamp) {
         fwrite(timeStampBuffer, stampLen, 1, outFile);
      }
      fwrite(logData.outBuff, logData.dataCount, 1, outFile);
      // If last character on line was not an EOL then generate one.
      // (Looks like emBOS always add one though.)
      if ( (! logData.lastIsEol) && doAutoNewline) {
         fprintf(outFile, "\n");
      }
   }
   if (logData.lastIsEol) {
      logData.showTimeStamp = true;
   }
   
}

void logDataOPGenerateOutput( )
{
   // TODO: Do some fancier handling of this non-protocol data that is received during boot of SMGW Step1-HW
   if (logData.dataCountOP > 0) {
      if (doConsoleOutput) {
         co->setColor(ConsoleColoring::green, ConsoleColoring::black);
         fwrite(logData.outBuffOP, logData.dataCountOP, 1, stdout);
         co->setDefColors();
      }
      if (outFile != NULL) {        
         fwrite(logData.outBuffOP, logData.dataCountOP, 1, outFile);
      }
   }

   logData.dataCountOP = 0;
}

const char *LOG_ERROR_MESSAGE_FORMAT = "[SegDebLogger: %s]\n";
void printPacketErrorMessage()
{
   if (doConsoleOutput) {
      co->setColor(ConsoleColoring::cyan, ConsoleColoring::black);
      printf(LOG_ERROR_MESSAGE_FORMAT, logData.packetMessageBuffer);
      co->setDefColors();
   }
   if (outFile != NULL) {
      fprintf(outFile, LOG_ERROR_MESSAGE_FORMAT, logData.packetMessageBuffer);
      fflush(outFile);
   }
}

//----------------------------------------------------------------------------
// Print response message from a probing command.
// Probing commands are like get task information, get stack usage, ..
//
void printProbeHandlingMessage()
{
   char timeStampBuffer[200];
   int stampLen = 0;
   bool doShowTimeStamp = false;
   if (logData.showTimeStamp) {
      if(doTimeStamps)
      {
         stampLen = formatTimeStamp(timeStampBuffer, sizeof(timeStampBuffer));
         doShowTimeStamp = true;
         // logData.showTimeStamp = false; (implicite newline, so no need to suppress timestamp)
      }
   }
   if (doConsoleOutput) {
      co->setColor(ConsoleColoring::dark_yellow, ConsoleColoring::black);
      if (doShowTimeStamp) {
         fwrite(timeStampBuffer, stampLen, 1, stdout);
      }
      fwrite(logData.packetMessageBuffer, strlen(logData.packetMessageBuffer), 1, stdout);
      co->setDefColors();
   }
   if (outFile != NULL) {
      if (doShowTimeStamp) {
         fwrite(timeStampBuffer, stampLen, 1, outFile);
      }
      fwrite(logData.packetMessageBuffer, strlen(logData.packetMessageBuffer), 1, outFile);
      fflush(outFile);
   }
}

//-----------------------------------------------------------------------------------------------------------------
//
// Add received data to the log-data we collect.
// This contains a state-machine that assembles full log-lines from the fragments we get on each call to this function.
// Currently this state-machine only handles the debug-printf kind of messages sent from Vader but could possibly be
// enhanced to also handle task and stack information. I assume this information is sent in a similar way on this 
// same serial line that we are reading.
// 
// The data consist of frames that start with two "lead-in" characters and ends with an end-of-frame character.
// After the lead-in sequence comes a length byte followed by some tag indicating what kind of frame it is.
// Each frame has a checksum byte before the end-of-frame character. The checksum is a simple one-byte sum of all bytes
// starting from and including the length-byte up to but not including the checksum.
// The tag indicating the frame is a debug-printf string is 0x43
//
void logDebugData( unsigned char *logDataBuffer, int bytesInBuffer)
{
    // printf("%5d bytes [", bytesInBuffer);
    int dataBufferIndex=0;
    unsigned char c;
    gotData += bytesInBuffer;

    while (dataBufferIndex < bytesInBuffer) {
        c = logDataBuffer[ dataBufferIndex++ ];

        // Simple error recovery: check for SD0 in other state than idle
        // If found then be prepared that following could be an SD1 and
        // previous packet was incomplete.
        if ( (c == LOGDATA_LeadIn_SD0) 
               && ((dataBufferIndex < bytesInBuffer) && (logDataBuffer[ dataBufferIndex ] == LOGDATA_LeadIn_SD1))
               && (logData.state >= LDState_StringData) ) {
            // Was last packet incomplete?
            logDataGenerateOutput( );
            logData.state = LDState_Idle;
        }

        // Collect the data into an intermediate output buffer.
        // Output the buffer when whole frame has been received.
        // (That should skip corrupted packages and reduce calls to printf)


        switch( logData.state ) {
            case LDState_Idle:
                // we are waiting for SD0. Skip other chars
                if ( c == LOGDATA_LeadIn_SD0 ) {
                    logData.state = LDState_SD0;
                    logData.cmdReceived = LDState_SD0; // Remember the actual command
                    logData.packetError = false;       // No errors in this packet yet
                    logData.packetMessageBuffer[0] = 0; // No messages to report either

                    if (doTimeStamps) {
                       logData.MakeTimeStamp();
                    }
                } else {
                   // TODO: Add handling of data received before embos has been setup. e.g. Boot-strap messages
                   // TODO: add an option for handling this
                   logOffProtocolData( c );

                   // In that function flush buffer on newlines
                   // In the logDataGenerateOutput() function before generating the normal logdata output check the
                   // new off-protocol-data buffer and flush it out first.
                   // TODO: Figure out if we should end up in this state and log off-protocol-data also in other conditions.
                   //       e.g. if target was rebooting in middle of a message, how could we detect that?
                   //       Would the boot messages just go into the normal buffer and bail out somehow?
                }
                break;

            case LDState_SD0:
                // We got SD0, waiting for SD1. Other chars aborts to idle.
                if ( c == LOGDATA_LeadIn_SD1 ) {
                    logData.state = LDState_SD1;
                }
                break;

            case LDState_SD1:
                // We got SD1, waiting for length byte. (Should we abort on EOD?)
                logData.dataCount = 0;
                logData.state = LDState_LenByte;
                logData.pkgLen = c;
                logData.chksum = c;
                logData.lastIsEol = false;
                break;

            case LDState_LenByte:
                // Got the length byte, now expect data type
                logData.state = LDState_LenByte;
                if ( logData.pkgLen > 0) {
                    if ( c == LOGDATA_STRTYPE ) {
                        logData.state = LDState_StringData;
                        logData.cmdReceived = LDState_StringData; // Remember the actual command
                        logData.pkgLen--;  // length included this type character, skip it.
                        logData.chksum += c;
                        if ( logData.pkgLen == 0 ) {       // Empty string?
                            logData.state = LDState_ChkSum;
                        }
                    } else if ( c == LOGDATA_LISTTYPE ) {
                       logData.state = LDState_List;
                       logData.pkgLen--;  // length included this type character, skip it.
                       logData.chksum += c;
                    } else if ( c == LOGDATA_TASK_INFO) {
                       logData.state = LDState_TaskInfo;
                       logData.cmdReceived = LDState_TaskInfo; // Remember the actual command
                       logData.pkgLen--;  // length included this type character, skip it. (40)
                       logData.chksum += c;
                       logData.taskInfoReceiveBuffer.Clear();
                    } else if (c == LOGDATA_STACK_PROBE) {
                       logData.state = LDState_StackProbe;
                       logData.cmdReceived = LDState_StackProbe; // Remember the actual command
                       logData.pkgLen--;  // length included this type character, skip it. (40)
                       logData.chksum += c;
                       logData.stackInfoReceiveBuffer.Clear();
                    } else {
                        // printf ("Unknown tag 0x%02x\n", c);
                        // Unknown data type, skip packet
                        logData.state = LDState_Idle;
                    }
                } else {
                    // A dummy packet?
                    // Skip it entirely by setting idle mode.
                    // No reason waiting for the EOD
                    logData.state = LDState_Idle;
                }
                break;

            case LDState_StringData:
               {                  
                  // Collecting data into output buffer
                  // Change state when all expected characters received.

                  // Enhancement: Terminate if encountered EOD character?
                  logDebugDataAddChar( c );
               }
               break;

            case LDState_List:
               if ( c == LOGDATA_LIST_TASKS ) {
                  logData.state = LDState_ListTasks;
                  logData.cmdReceived = LDState_ListTasks; // Remember the actual command
                  logData.numTaskPointersReceived = 0;
                  logData.numTaskPointerBytesReceived = 0;
                  logData.pkgLen--;  // length included this type character, skip it.
                  logData.chksum += c;
               } else {
                  // TODO: error handling of unknown list-type
               }
               break;

            case LDState_ListTasks:
               // printf ("List tasks byte 0x%02x\n", c);
               logData.pkgLen--;  // length included this type character, skip it.
               logData.chksum += c;
               if (logData.numTaskPointerBytesReceived < logData.MaxTasks*4) {
                  logData.taskPointersBuffer[logData.numTaskPointerBytesReceived ] = c;
                  logData.numTaskPointerBytesReceived ++;
               }
               if (logData.pkgLen == 0) {
                  logData.state = LDState_ChkSum;
               }
               break;

            case LDState_TaskInfo:
               // printf ("Task info byte 0x%02x\n", c);
               logData.pkgLen--;  // length included this type character, skip it.
               logData.chksum += c;
               logData.taskInfoReceiveBuffer.AppendDataU8(c); // updates receive count

               if (logData.pkgLen == 0) {
                  logData.state = LDState_ChkSum;
               }
               break;

            case LDState_StackProbe:
               // printf ("Task info byte 0x%02x\n", c);
               logData.pkgLen--;  // length included this type character, skip it.
               logData.chksum += c;
               logData.stackInfoReceiveBuffer.AppendDataU8(c); // updates receive count

               if (logData.pkgLen == 0) {
                  logData.state = LDState_ChkSum;
               }
               break;

            case LDState_ChkSum:
                // Got all data, now we have the checksum.
                // Check it and discard ?
                if ( logData.chksum != c ) {
                   sprintf_s(logData.packetMessageBuffer, sizeof(logData.packetMessageBuffer), "Checksum error (sum %02X, received %02X)", logData.chksum, c);
                   // Try to recover in the special case that this was a reboot interrupting a string message
                   if (logData.cmdReceived == LDState_StringData) {
                      logDataGenerateOutput( );
                      printPacketErrorMessage( );

                   } else {
                      printPacketErrorMessage( );
                   }

                   // Push back the character into the buffer and handle it again, could be part of new packet
                   dataBufferIndex--;

                   logData.cmdReceived = LDState_Idle;
                   logData.packetError = true;
                   logData.state = LDState_Idle; // Abort packet

                } else {
                  logData.state = LDState_EOD;
                }
                break;

            case LDState_EOD:
                // Last byte of package.
                if ( c == LOGDATA_EOD ) {
                   if (logData.cmdReceived == LDState_ListTasks) {
                      ServiceHandler_ListTasks( );
                   } else if (logData.cmdReceived == LDState_TaskInfo) {
                      ServiceHandler_TaskInfo( );
                   } else if (logData.cmdReceived == LDState_StackProbe) {
                      ServiceHandler_StackProbe( );
                   } else {
                      logDataGenerateOutput( );
                   }

                } else if ( ! logData.packetError ) {
                   if (logData.cmdReceived == LDState_StringData) {
                      logDataGenerateOutput( );
                      // Push back the character into the buffer and handle it again, with new state
                      dataBufferIndex--;
                   }

                   sprintf_s(logData.packetMessageBuffer, sizeof(logData.packetMessageBuffer), "End of packet error (EOD=%02X, received %02X)", LOGDATA_EOD, c);
                   printPacketErrorMessage();
                }

                logData.state = LDState_Idle;
                logData.cmdReceived = LDState_Idle; // Remember the actual command

                break;
        }
    }

    if (logData.state == LDState_Idle) {
       if (logData.taskListerState == logData.TaskLister_StartListAll) {
          // Set task to list to first one
          logData.taskLister_listTaskNum = 0;
          if (logData.numTaskPointersReceived > 0) {
             // Set state to listing task, then call for the task
             logData.taskListerState = logData.TaskLister_ListingTasks; // When task is received state changes to "next"
             if (doVerboseMessages > 0) {
                printf("Request for task info %d\n", logData.taskLister_listTaskNum );
             }
             WriteCommand_GetTaskInfo_ByIndex( logData.taskLister_listTaskNum );
          }

       } else if (logData.taskListerState == logData.TaskLister_ListStack) {
       } else if (logData.taskListerState == logData.TaskLister_ListNext) {
          // Set task to list to next one
          logData.taskLister_listTaskNum ++;
          if (logData.taskLister_listTaskNum >= logData.numTaskPointersReceived ) {
             logData.hasTimeStamp = true; // Reuse previous timestamp, this is part of that message anyway
             sprintf_s(logData.packetMessageBuffer, sizeof(logData.packetMessageBuffer), 
                "Done with task info (%d/%d)\n", logData.numTaskPointersReceived, logData.taskLister_listTaskNum );
             printProbeHandlingMessage();
             logData.taskListerState = logData.TaskLister_Done;
          } else {
             logData.taskListerState = logData.TaskLister_ListingTasks; // When task is received state changes to "next"
             if (doVerboseMessages > 0) {
                printf("Request for task info %d\n", logData.taskLister_listTaskNum );
             }
             WriteCommand_GetTaskInfo_ByIndex( logData.taskLister_listTaskNum );
          }
       } else {
       }
    }


    // printf("]\n", bytesInBuffer);
}

//-------------------------------------------------------------------------------------------------
// handle response of type task-list
//
void ServiceHandler_ListTasks( )
{
   // List the tasks we got.
   int i=0;
   int ptrNr;
   unsigned int taskPtr;
   logData.numTaskPointersReceived = logData.numTaskPointerBytesReceived / 4;
   
   sprintf_s(logData.packetMessageBuffer, sizeof(logData.packetMessageBuffer), 
      "Number of tasks: %3d\n", logData.numTaskPointersReceived);
   printProbeHandlingMessage();

   for (ptrNr=0 ; ptrNr < logData.numTaskPointersReceived ; ptrNr++, i+=4) {
      taskPtr = 0;
      taskPtr |= ((unsigned int)(logData.taskPointersBuffer[i + 0])) << 24;
      taskPtr |= ((unsigned int)(logData.taskPointersBuffer[i + 1])) << 16;
      taskPtr |= ((unsigned int)(logData.taskPointersBuffer[i + 2])) <<  8;
      taskPtr |= ((unsigned int)(logData.taskPointersBuffer[i + 3]));
      if (doVerboseMessages > 0) {
         printf("Task: 0x%08x\n", taskPtr);
      }
   }

   logData.state = LDState_Idle;
   logData.cmdReceived = LDState_Idle; // Remember the actual command

   // Initiate state-machine to request task infos for all tasks.
   logData.taskListerState = logData.TaskLister_StartListAll;
}

//-------------------------------------------------------------------------------------------------
// handle response of type task-info
//
void ServiceHandler_TaskInfo( )
{
   unsigned int dataU32;
   unsigned short dataU16;
   unsigned int offs = 0;

   unsigned char nameBuff[31]; 
   unsigned int stackBase;
   unsigned int stackSize = 0;
   unsigned int execTime;
   unsigned int osTime;
   unsigned int cycles;

   if (logData.taskInfoReceiveBuffer.numReceivedBytes < 4) {
      // "not a task" ?
      printf("TaskInfo: response too short (%d)\n", logData.taskInfoReceiveBuffer.numReceivedBytes);

   } else {

      // U32 cycles
      dataU32 = logData.taskInfoReceiveBuffer.GetDataU32(offs);
      cycles = dataU32;
      offs += 4;

      // String  - Task name (maximum 30chars). First byte is length, followed by that many characters of the name.
      memset(nameBuff, 0, sizeof(nameBuff));
      int objectLen = logData.taskInfoReceiveBuffer.GetDataString(offs, nameBuff, sizeof(nameBuff));
      offs += objectLen;

      // U8   - prio
      offs += 1;
      // U8   - Status
      offs += 1;
      // U32  - Data (wait list)
      offs += 4;
      // U32  - timeout
      offs += 4;
      // U32  - stackbase
      dataU32 = logData.taskInfoReceiveBuffer.GetDataU32(offs);
      stackBase = dataU32;
      offs += 4;

      // U32  - exec time
      dataU32 = logData.taskInfoReceiveBuffer.GetDataU32(offs);
      execTime = dataU32;
      offs += 4;

      // U32  - num activations
      offs += 4;

      // U16  - stacksize high
      dataU16 = logData.taskInfoReceiveBuffer.GetDataU16(offs);
      stackSize = dataU16;
      offs += 2;
      if ( (dataU16 & 0xFF00) == 0xFF00 ) {
         // U16  - stacksize low (if previous high byte was FF)
         dataU16 = logData.taskInfoReceiveBuffer.GetDataU16(offs);
         stackSize = ((stackSize & 0xFF) << 16) | dataU16;
         offs += 2;
      }

      // U8   - time slice rem
      offs += 1;
      // U8   - timeslice reload
      offs += 1;

      // U16  - OS_GetTime
      dataU16 = logData.taskInfoReceiveBuffer.GetDataU16(offs);
      osTime = dataU16;
      offs += 2;

      if (doVerboseMessages > 0) {

         printf("TaskInfo: task name = %s\n", nameBuff);
         printf("TaskInfo: Cycles    = %10u\n", cycles);
         printf("TaskInfo: OS Time   = %10u\n", osTime);
         printf("TaskInfo: exec time = %10u\n", execTime);
         printf("TaskInfo: stack base= 0x%08x\n", stackBase);
         printf("TaskInfo: stack len = %10u\n", stackSize);
      }
   
      if (sizeof(nameBuff) <= sizeof(logData.currTaskName)) {
        memcpy(logData.currTaskName, nameBuff, sizeof(nameBuff));
      } else {
         fprintf(stderr, "Error: logData.currTaskName size is too small\n");
         exit(1);
      }
      logData.currStackSize = stackSize;

      // logData.taskListerState = logData.TaskLister_ListNext;
      logData.taskListerState = logData.TaskLister_ListStack;
      if (stackSize > 0xFFFF) {
         stackSize = 0xFFFF;
      }
      if ( stackSize > 0 ) {
         WriteCommand_GetStackCheck( stackBase, stackSize);
      }
   }
}

void ServiceHandler_StackProbe( )
{
   unsigned short dataU16;
   unsigned int offs = 0;
   unsigned int usedSize;

   dataU16 = logData.stackInfoReceiveBuffer.GetDataU16(offs);
   usedSize = logData.currStackSize - dataU16;  // Make it show in same way as in IAR debugger
   sprintf_s(logData.packetMessageBuffer, sizeof(logData.packetMessageBuffer), 
      "Stack probe %2d: %5u / %5u, \"%s\"\n", logData.currTaskIndex, usedSize, logData.currStackSize, logData.currTaskName);
   printProbeHandlingMessage();
   logData.taskListerState = logData.TaskLister_ListNext;
}

//-------------------------------------------------------------------------------------------------
// Collecting data into output buffer
// Change state when all expected characters received.
void logDebugDataAddChar( unsigned char dc )
{
   logData.chksum += dc;

   logData.lastIsEol = false;
   if (dc == 0x0a) {
      logData.lastIsEol = true;
   } else if (dc < 0x20) {
      dc = '_'; // only displayable characters          NOTE: here we could alternatively show it in hex as in logOffProtocolData() !!
   }

   if (logData.dataCount <= sizeof(logData.outBuff)) {
      logData.outBuff[ logData.dataCount ++ ] = dc;
   } else {
      // We should not really end up here.
      //   logDataGenerateOutput();
   }

   if ( logData.dataCount == logData.pkgLen ) {
      // got all payload characters
      logData.state = LDState_ChkSum;
   }
}

void logOffProtocolData(unsigned char dc )
{
   bool skipChar = false;

   // logData.lastIsEol = false;
   if ((dc == 0x0a) || (dc == 0x0d)) {
      // logData.lastIsEol = true;
   } else if (dc < 0x20) {
      
      if ((logData.dataCountOP+4) <= sizeof(logData.outBuffOP)) {
         // dc = '_'; // only displayable characters
         char hxBuff[5];
         _snprintf_s(hxBuff, sizeof(hxBuff), _TRUNCATE, "<%02x>", dc);
         logData.outBuffOP[ logData.dataCountOP ++ ] = (unsigned char)(hxBuff[0]);
         logData.outBuffOP[ logData.dataCountOP ++ ] = (unsigned char)(hxBuff[1]);
         logData.outBuffOP[ logData.dataCountOP ++ ] = (unsigned char)(hxBuff[2]);
         logData.outBuffOP[ logData.dataCountOP ++ ] = (unsigned char)(hxBuff[3]);

      } else {
         // Just skip it silently
      }
      skipChar = true;
   } 
   
   if ( (!skipChar) && (logData.dataCountOP <= sizeof(logData.outBuffOP))) {
      
      logData.outBuffOP[ logData.dataCountOP ++ ] = dc;

   } else {
      // We should not really end up here.
      //   logDataGenerateOutput();
   }

   if (dc == 0x0a) {
      // TODO: flush out the buffer data
      logDataOPGenerateOutput( );
   }

}

//--------------------------------------------------------------------------------------------------------------------------
// Generate commands for accessing the internal information of embos tasks.
//
#define PROT_SD0  0xedU
#define PROT_SD1  0x8cU
#define PROT_ED   0x8dU
// const int transmitBufferSize = 200;
// unsigned char transmitBuffer[ transmitBufferSize ];

// C - send to application
// s   Get system info
// t   Get task info
// l   Get task list
// c   Check stack
// b, w, 1,2,4 memory read write

int WriteCommand_GetTaskList()
{
   if (doVerboseMessages > 0) {
      printf("REQUEST FOR TASK-LIST\n");
   }

   const unsigned char transmitBuffer[ ] = {
      PROT_SD0, PROT_SD1, 1,   // Length of remaining data, set  1  to checksum (--> 1)
      108,                     // 'l' - Command to send           add 108 to checksum (--> 109)
      147,                     // Checksum                  add to checksum and it should become zero (i.e. -109 = 147)
      PROT_ED};// INSTAGE_ED
   return WriteCommand(transmitBuffer, sizeof(transmitBuffer));
}

int WriteCommand_GetTaskInfo_ByIndex(int taskIndex)
{
   unsigned int taskPtr = 0;

   if (taskIndex >= logData.numTaskPointersReceived) {
      return 0;
   }
   logData.currTaskIndex = taskIndex;
   unsigned int taskPtrOffset = taskIndex*4;

   unsigned char csum = 0;
   static unsigned char transmitBuffer[10];
   transmitBuffer[0] = PROT_SD0;
   transmitBuffer[1] = PROT_SD1;
   csum += (transmitBuffer[2] = 1+4);                     // Length of remaining data, set  5  to checksum
   csum += (transmitBuffer[3] = 116);                     // 't' - Command to send = 116. and add to checksum
   csum += (transmitBuffer[4] = logData.taskPointersBuffer[taskPtrOffset + 0]);
   csum += (transmitBuffer[5] = logData.taskPointersBuffer[taskPtrOffset + 1]);
   csum += (transmitBuffer[6] = logData.taskPointersBuffer[taskPtrOffset + 2]);
   csum += (transmitBuffer[7] = logData.taskPointersBuffer[taskPtrOffset + 3]);
   transmitBuffer[8] = -csum;                             // Checksum                  add to checksum and it should become zero (i.e. -109 = 147)
   transmitBuffer[9] = PROT_ED;// INSTAGE_ED

   if (doVerboseMessages > 0) {
      printf("REQUEST FOR TASK INFO %d, 0x%02x%02x%02x%02x\n",
         taskIndex,
         transmitBuffer[4],
         transmitBuffer[5],
         transmitBuffer[6],
         transmitBuffer[7]);
   }

   return WriteCommand(transmitBuffer, 10);
}

//
// Use as WriteCommand_GetStackCheck( stackBase, stackSize);
// Where the stackBase and stackSize could be the values that ServiceHandler_TaskInfo() gets.
//
int WriteCommand_GetStackCheck(unsigned int stackPtr, unsigned int checkLength)
{
   unsigned char csum = 0;
   static unsigned char transmitBuffer[12];

   transmitBuffer[0] = PROT_SD0;
   transmitBuffer[1] = PROT_SD1;
   csum += (transmitBuffer[2] = 1+4+2);                   // Length of remaining data, set  5  to checksum
   csum += (transmitBuffer[3] = 99);                      // 'c' - Command to send = 99. and add to checksum
   csum += (transmitBuffer[4] = (unsigned char)((stackPtr >> 24) & 0xff));
   csum += (transmitBuffer[5] = (unsigned char)((stackPtr >> 16) & 0xff));
   csum += (transmitBuffer[6] = (unsigned char)((stackPtr >>  8) & 0xff));
   csum += (transmitBuffer[7] = (unsigned char)((stackPtr      ) & 0xff));
   csum += (transmitBuffer[8] = (unsigned char)((checkLength >>  8) & 0xff));
   csum += (transmitBuffer[9] = (unsigned char)((checkLength      ) & 0xff));
   transmitBuffer[10] = -csum;                             // Checksum                  add to checksum and it should become zero (i.e. -109 = 147)
   transmitBuffer[11] = PROT_ED;// INSTAGE_ED

   if (doVerboseMessages > 0) {
      printf("REQUEST FOR STACK PROBE at  0x%02x%02x%02x%02x, max %d bytes\n",
         transmitBuffer[4],
         transmitBuffer[5],
         transmitBuffer[6],
         transmitBuffer[7],
         checkLength);
   }

   return WriteCommand(transmitBuffer, 12);
}
