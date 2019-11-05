import getopt
import sys
import dlms.parser


def usage():
    print ("")
    print ("Marvin Spec obis extractor, generates obiscode defintion from specification file")
    print ("")
    print ("usage:")
    print ("specToObisSrc.py --specInput=<name of xml with object model specification> (default '')")
    print ("                 --outputFile=<generated header file> (default 'Cfg_ObisInitValues.hpp'");
    print ("                 --verbose (outputs additional information)")
    
    
def main():
    # this works when run from the _Msim/pylib/tests directory
    specInputFile = "../../../../doc/object_model/ObjectSpec.xml" 
    outputFile = 'Cfg_ObisInitValues.hpp'
    #verbose = 0
    
    try:
        opts, args = getopt.getopt(sys.argv[1:], "", ["help", "specInput=", "outputFile=", \
                                                      "verbose"])
    except getopt.GetoptError as err:
        # print help information and exit:
        print(err) # will print something like "option -a not recognized"
        usage()
        sys.exit(2)
    if len(args) > 0:
        usage()
        sys.exit(2)
    for o, a in opts:
        if o in ("--help"):
            usage()
            sys.exit()
        elif o in ("--specInput"):
            specInputFile = a
        elif o in ("--outputFile"):
            outputFile = a
        elif o in ("--verbose"):
            pass
            #verbose = 1
        else:
            assert False, "unhandled option"


    # Read the DLMS object model using the default settings
    os = dlms.parser.objectSpec( xmlFile=specInputFile)
    
    # Open file for the generated output
    f = open(outputFile, 'w')
    
    # for each object in the object model ....
    for objname in os.getVisualNameList():
    
        objIter = os.getObject(objname)
        obisHex = objIter.getObishex()
        obisBin = bytes.fromhex(obisHex)
        obisCode = objIter.getObisCode()
        # Generate a c-syntax initialization string.
        # It should be like: {0x01,0x00,0x04,0x08,0x00,0xff}
        # There must be exactly 6 items in the initializer.
        # If we could be sure the hex string is correctly formatted from the beginning we could
        # use some syntax like: for k in bytes.fromhex(obisHex): print("{0:02x},".format(k))
        # If there is an error in the ObjectSpec source we want to trap it here though and not during
        # use of file generated here. (i.e. not during build time but before).    
        obisCHex = '{' + "0x{a:02x},0x{b:02x},0x{c:02x},0x{d:02x},0x{e:02x},0x{f:02x}".format(
            a = obisBin[0],
            b = obisBin[1],
            c = obisBin[2],
            d = obisBin[3],
            e = obisBin[4],
            f = obisBin[5]) + '}'
    
        print("#define OBISCODE_{objectName:<44} {cInitVal} // {obisHex} {obisCode}".format(
                     objectName=objname,
                     cInitVal=obisCHex,
                     obisCode=obisCode,
                     obisHex=obisHex),file=f)


if __name__ == "__main__":
    main()
