SegDebLogger, a debug logger for seggers emBOS.
Version 1.0
Reads from the serial line the debug output and shows on screen or writes in file.

Usage: SegDebLogger [-cCOMPORT]|[-sSPEED]|[-fFILE]|[-h]|[-n]
 -h : show this message
 -fFILE : write to FILE as well
 -q     : don't log on console (meaningful only with -f option)
 -cPORT : specify port name to use with serial interface (COM10 would be used now)
 -sSPEED : specify speed to use with serial interface (115200 would be used now)
 -n     : do not add newline at end of message

To stop the application, press Ctrl-C or Ctrl-Break



Example use:
	SegDebLogger -fTst.txt
That would start logging from the serial port to both console and the file Tst.txt
Press Ctrl-C to stop.

NOTE!!!!
Started without any parameters just starts logging (from COM10 and baudrate 115200)
If the cable is not attached or Vader not in debug mode and/or Vaders "severity" variable
set, then it may appear as if the program doesn't do anything.

If debug messages was were generated with "\n" at end of the string passed to 
OS_SendString(), then use -n option. (Vader did not have that but Nova does.)

For debuggingn in Visual Studio, add e.g. following arguments to Command Arguments
in debug section of project properties:
	-cCOM1 -s115200 -t -n  -fc:\Temp\SegLog_Com1_b.txt
