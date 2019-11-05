import getopt
import sys
import dataObjectElaborationLib.parseFiles
import dataObjectElaborationLib.writeFiles


def usage():
    print ("")
    print ("Marvin Data object elaboration script")
    print ("")
    print ("usage:")
    print ("dataObjectElaboration.py --inputDirs=<list of directories> (default '')")
    print ("                         --outputDir=<output location> (default '../meter_config/MOS_DataObjectConfig_Combined'");
    print ("                         --dbName=<name of database> (default 'elaboration.db')")
    print ("                         --overwriteDb (removes the initial content of the database)");
    print ("                         --isExtension (indicates that this is the elaboration of the extension image)")
    print ("                         --verbose (outputs additional information)")
    
    
def main():
    dbName = "elaboration.db"  
    inputDirs = ""
    outputDirname = '../meter_config/MOS_DataObjectConfig_Combined'
    overwriteDb = 0
    isExtension = 0
    verbose = 0
    
    try:
        opts, args = getopt.getopt(sys.argv[1:], "", ["help", "inputDirs=", "outputDir=", \
                                                      "dbName=", "overwriteDb", "isExtension", \
                                                      "verbose"])
    except getopt.GetoptError as err:
        # print help information and exit:
        print(err) # will print something like "option -a not recognized"
        usage()
        sys.exit(2)
            
    for o, a in opts:
        if o in ("--help"):
            usage()
            sys.exit()
        elif o in ("--inputDirs"):
            inputDirs = a
        elif o in ("--outputDir"):
            outputDirname = a
        elif o in ("--dbName"):
            dbName = a
        elif o in ("--overwriteDb"):
            overwriteDb = 1
        elif o in ("--isExtension"):
            isExtension = 1
        elif o in ("--verbose"):
            verbose = 1
        else:
            assert False, "unhandled option"
            
    if (len(args) > 0) or (inputDirs == ""):
        usage()
        sys.exit(2)

    print("Elaborating data objects ...")
    dataObjectElaborationLib.parseFiles.main(verbose, inputDirs, dbName, overwriteDb, isExtension)
    dataObjectElaborationLib.writeFiles.main(verbose, outputDirname, dbName, isExtension)
    print("   ... done")
    exit(0)
    
if __name__ == "__main__":
    main()
    