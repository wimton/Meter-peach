####################################################################################################
#
#  Elaborates Marvin Persistent Object Code
#
####################################################################################################


import re
import os
import glob
import sqlite3

re_w = "[ ]*([a-zA-Z0-9_]+)[ ]*";              # regular expression to match a word
re_w_type = "[ ]*([a-zA-Z0-9_:\[\]\*]+)[ ]*";  # regular expression to match a word with colons

classList = []                    # list containing all persistent object classes 
locationDict = {}                 # contains the location of a class in the source code  
parentDict = {}                   # contains the parent of a class
versionDict = {}                  # contains the version of a class
attribDict = {}                   # contains the attributes of a class
instanceList = []                 # contains all class instances
               
def uniquify(seq):
    # Not order preserving
    keys = {}
    for e in seq:
        keys[e] = 1
    return list(keys.keys())


def listSearch(seq, item):
    for e in seq:
        if e == item:
            return 1
    return None


def getClassIdFromName(cursor, name):
    sql = "SELECT classId from tb_classes WHERE name = '{0}'".format(name)
    cursor.execute(sql)
    classId = cursor.fetchone()
    if not classId:
        return 0
    return classId[0]

####################################################################################################

def parseFile_item(fileName, re1, re2, mode):
    global verbose
    global classList
    global parentDict
    global locationDict
    global versionDict
    global attribDict
    error = 0
    itemFound = 0
    braceLevel = 0
    braceItemClass = {}
    braceItemClass[0] = "none"
    parentClass = {}
    parentClass[0] = "none"
    parentClassLine = {}
    parentClassLine[0] = "0"
    
    #print ("parsing file {0} ...".format(fileName))
    f = open(fileName, 'r')
    
    lineNr = 1;
    for line in f:

        ############################################################################################
        # looking for the class the macros are in
        if(mode != "prescan"):
            re0 = re.compile("(^| )+class[ ]+" + re_w + "([ :]+|$)(.*$)")
            m = re0.findall(line)
            if (m):
                #for item in m[0]:
                #    print("'{0}'".format(item), end = " ")
                #print("")
                braceItemClass[braceLevel+1] = m[0][1]
                parentClass[braceLevel+1] = "none"
                re0 = re.compile("public[ ]+" + re_w + "($|[ ,]+)")
                m = re0.findall(line) 
                if (m):
                    #for item in m:
                    #    print("'{0}'".format(item), end = " ")
                    #print("")
                    parentClass[braceLevel+1] = m[0][0]
                    parentClassLine[braceLevel+1] = lineNr
            else:
                re0 = re.compile("{")
                m = re0.findall(line)
                if (m):
                    braceLevel += len(m)
                    #print("( {0}".format(braceLevel))
                re0 = re.compile("}")
                m = re0.findall(line)
                if (m):
                    braceLevel -= len(m)
                    #print(") {0}".format(braceLevel))
        
        ############################################################################################
        # looking for the macros
        m = re1.findall(line)
        if (m):
            # remove lines containing #define statements
            re0 = re.compile("#define[ ]+")
            m = re0.findall(line)
            if (not m):
                if(mode == "prescan"):
                    # we are only searching for the keyword
                    # we also ignore all the define statements
                    itemFound = 1
                
                else:    
                    m = re2.findall(line)
                    if (m):
                        className = braceItemClass[braceLevel]
                        parentClassName = parentClass[braceLevel]
                        classLineNr = parentClassLine[braceLevel]
                        if not listSearch(classList,className):
                            classList.append(className)
                            attribDict[className] = []
                            parentDict[className] = parentClassName
                            locationDict[className] = [fileName, classLineNr]
                        if (mode == "config"):
                            config = m[0][1]
                            if (config != "Config_t"):
                                print("  {0}".format(line), end = '')
                                print("{0}:{1}:1: Error: Config must be named 'Config_t'".format(fileName, lineNr))
                                error = 1
                        if (mode == "version"):
                            versionDict[className] = m[0][1]
                        if (mode == "attribute"):
                            attrName = m[0][1]
                            #print(attrName)
                            if listSearch(attribDict[className],attrName):
                                print("  {0}".format(line), end = '')
                                print("{0}:{1}:1: Error: duplicate attribute".format(fileName, lineNr))
                                error = 1
                            attribDict[className].append(attrName)
                            attribDict[className,attrName] = [m[0][2],m[0][3]]
                        
                        if verbose != 0:
                            print(className, end = " ")
                            print(parentClassName, end = " ")
                            for item in m[0]:
                                print("'{0}'".format(item), end = " ")
                            print("")
                            
                        itemFound = 1
                    else:
                        print("  {0}".format(line), end = '')
                        print("{0}:{1}:1: Error: incorrect line format".format(fileName, lineNr))
                        error = 1
        lineNr += 1;
    
    f.close()
    if (error ==1):
        exit(1)
    return itemFound


####################################################################################################

def parseFile_registerItem(fileName):
    global verbose
    global classList
    global instanceList
    error = 0
    itemFound = 0
    classType = {}
    
    re1 = re.compile("^[ \t]*MOS_DO_REGISTER_ITEM[ (]+")
    re2 = re.compile("^[ \t]*(MOS_DO_REGISTER_ITEM)[ ]*[(]" + re_w + "," + re_w + "[)]")
    
    #print ("parsing file {0} ...".format(fileName))
    f = open(fileName, 'r')
    
    lineNr = 1;
    for line in f:

        ############################################################################################
        # looking for class instances
        for item in classList:
            re0 = re.compile("(^| )+" + item + re_w + ";")
            m = re0.findall(line)
            if (m):
                #for item2 in m[0]:
                #    print("'{0}'".format(item2), end = " ")
                #print("")
                classType[m[0][1]] = item;
       
        ############################################################################################
        # looking for the macros
        m = re1.findall(line)
        if (m):
            m = re2.findall(line)
            if (m):
                instanceName = m[0][1]
                try:
                    if verbose != 0:
                        print (classType[instanceName], end = ' ')
                        for item in m[0]:
                            print("'{0}'".format(item), end = " ")
                        print("")
                    itemFound = 1
                    instanceList.append([classType[instanceName], m[0][1], m[0][2]])
                except:
                    print("  {0}".format(line), end = '')
                    print("{0}:{1}:1: Error: {2} is not a valid object".format(fileName, lineNr,
                                                                                      instanceName))
                    error = 1
            else:
                print("  {0}".format(line), end = '')
                print("{0}:{1}:1: Error: no valid type".format(fileName, lineNr))
                error = 1
        lineNr += 1;
    
    f.close()
    if (error ==1):
        exit(1)
    return itemFound

####################################################################################################

####################################################################################################
#
#
#  MAIN
#
#
####################################################################################################
def main(_verbose, inputDirs, dbFile, _overwriteDb, _isExtension):
    global verbose
    global isExtension
    global overwriteDb
    overwriteDb = _overwriteDb
    isExtension = _isExtension
    verbose = _verbose

    fileList = []
    for item in inputDirs.split(' '):
        fileList.extend(glob.glob("{0}*".format(item)))
    
    classFileList = []
    instanceFileList = []
    
    ################################################################################################
    #  PASS 1 finding files with keywords
    ################################################################################################
    for fileName in fileList:
           
        name, ext = os.path.splitext(os.path.basename(fileName))
        if ((ext != ".c") and (ext != ".cpp") and (ext != ".h") and (ext != ".hpp")):
            continue
    
        re1 = re.compile("^[ \t]*(MOS_DO_ATTR[ (]+|MOS_DO_CONFIG[ (]+|MOS_DO_VERSION[ (]+)")
        result = parseFile_item(fileName, re1, re1, "prescan")
        if (result == 1):
            classFileList.append(fileName)
            
        re1 = re.compile("^[ \t]*MOS_DO_REGISTER_ITEM[ (]+")
        result = parseFile_item(fileName, re1, re1, "prescan")
        if (result == 1):
            instanceFileList.append(fileName)
        
    ################################################################################################
    #  PASS 2 finding persistent object classes
    #
    #  in this step we also produce the list of persistent object classes 'classList'
    ################################################################################################
    if verbose != 0:
        print () 
    for fileName in classFileList:
        
        re1 = re.compile("^[ \t]*MOS_DO_ATTR[ (]+")
        re2 = re.compile("^[ \t]*(MOS_DO_ATTR)[ ]*[(]" + re_w + "," + re_w + "," + re_w_type + "[)]")
        result = parseFile_item(fileName, re1, re2, "attribute")
          
        re1 = re.compile("^[ \t]*MOS_DO_CONFIG[ (]+")
        re2 = re.compile("^[ \t]*(MOS_DO_CONFIG)[ ]*[(]" + re_w + "[)]")
        result = parseFile_item(fileName, re1, re2, "config")
        
        re1 = re.compile("^[ \t]*MOS_DO_VERSION[ (]+")
        re2 = re.compile("^[ \t]*(MOS_DO_VERSION)[ ]*[(]" + re_w + "[)]")
        result = parseFile_item(fileName, re1, re2, "version")
    
    
    ################################################################################################
    #  PASS 3 finding persistent object instances
    ################################################################################################
    if verbose != 0:
        print () 
    for fileName in instanceFileList:     
        pass     
        result = parseFile_registerItem(fileName)
    
    
    ################################################################################################
    if verbose != 0:
        print () 
        for fileName in classFileList:
            print (fileName)  
        
        print () 
        for fileName in instanceFileList:
            print (fileName)   
      
      
    ################################################################################################
    # connect to database
    ################################################################################################
    
    if overwriteDb == 1:
        # recreate the file to get rid of history
        try:
            os.remove(dbFile)
        except:
            pass
        
    connection = sqlite3.connect(dbFile)
    cursor = connection.cursor()
    
    sql = "BEGIN EXCLUSIVE TRANSACTION"
    cursor.execute(sql)
    
    # create tables
    sql = "CREATE TABLE IF NOT EXISTS tb_files("   "kind TEXT, " \
                                                   "fileName TEXT)"
    cursor.execute(sql)
    
    sql = "CREATE TABLE IF NOT EXISTS tb_classes(" "classId INTEGER PRIMARY KEY, " \
                                                    "name TEXT, " \
                                                    "parentId INTEGER, " \
                                                    "isExtension INTEGER, " \
                                                    "version INTEGER)"
    cursor.execute(sql)
    
    sql = "CREATE TABLE IF NOT EXISTS tb_attributes(" "attributeId INTEGER PRIMARY KEY, " \
                                                      "name TEXT, " \
                                                      "classId INTEGER, " \
                                                      "type TEXT, " \
                                                      "storageClass TEXT)"
    cursor.execute(sql)
    
    sql = "CREATE TABLE IF NOT EXISTS tb_instances(" "instanceId INTEGER PRIMARY KEY, " \
                                                     "name TEXT, " \
                                                     "classId INTEGER, " \
                                                     "configName TEXT, " \
                                                     "isExtension INTEGER)"
    cursor.execute(sql)
    
      
    ################################################################################################
    # Verify consistency
    ################################################################################################
    if verbose != 0:
        print() 
    for cl in classList:
        if 0:
            print(cl, end = " ")
    
        if not listSearch(classList, parentDict[cl]):  
            # class is not in the current class list
            sql = "SELECT name from tb_classes WHERE name = '{0}'".format(parentDict[cl])
            cursor.execute(sql)
            if not cursor.fetchone():
                # class is not in the database 
                if cl != "MOS_Do_t":
                    # class is not root class
                    print("\n{0}:{1}:1: Error: '{2}' is not a valid data object parent ".format(
                                                                                locationDict[cl][0],
                                                                                locationDict[cl][1],
                                                                                parentDict[cl]))
                    exit(1)
                    
        if not listSearch(versionDict.keys(), cl):  
            # class has no version  specified
            print("\n{0}:{1}:1: Error: '{2}' has no version defined ".format(locationDict[cl][0],
                                                                             locationDict[cl][1],
                                                                             cl))
            exit(1)
            
    
        if 0:
            print(parentDict[cl], end = " ")
            print(versionDict[cl], end = " ")
            print() 
            for at in attribDict[cl]:
                print("   ", end = " ")
                print(at, end = " ")
                print(attribDict[cl,at], end = " ")
                print()  
    if 0:       
        for inst in instanceList:
            print(inst)    
        
        
    ################################################################################################
    # Update database
    ################################################################################################
    
    ################################################################################################  
    # insert classes into database
    
    for cl in classList:
        sql = "SELECT name from tb_classes WHERE name = '{0}'".format(cl)
        cursor.execute(sql)
        if not cursor.fetchone():
            # class is not already in the database
            sql = "INSERT INTO tb_classes (name) VALUES('{0}')".format(cl)
            #print(sql)
            cursor.execute(sql)
                 
            
    ################################################################################################      
    # insert parents / config / attributes
    
    for cl in classList:   
        parentId = getClassIdFromName(cursor, parentDict[cl])  
        sql = "UPDATE tb_classes SET parentId = {0}, version = '{1}', isExtension = {2} " \
              "WHERE name = '{3}'".format(parentId, versionDict[cl], isExtension, cl)
        #print(sql)
        cursor.execute(sql)
          
        # insert attributes
        
        classId = getClassIdFromName(cursor, cl)
        for at in attribDict[cl]:    
            sql = "SELECT name from tb_attributes WHERE name = '{0}' AND classId = {1}".format(at, classId)
            cursor.execute(sql)
            if not cursor.fetchone():
                # attribute is not already in the database
                sql = "INSERT INTO tb_attributes (name, classId) VALUES('{0}', {1})".format(at, classId)
                #print(sql)
                cursor.execute(sql)
                      
            sql = "UPDATE tb_attributes SET storageClass  = '{0}', type = '{1}' " \
                  "WHERE name = '{2}' AND classId = {3}".format(attribDict[cl,at][0] ,attribDict[cl,at][1], at, classId)
            #print(sql)
            cursor.execute(sql)
             
    
    
    ################################################################################################  
    # insert instances
    for inst in instanceList:
        typeName, name, configName = inst
        
        sql = "SELECT name from tb_instances WHERE name = '{0}'".format(name)
        cursor.execute(sql)
        if not cursor.fetchone():
            # instance is not already in the database
            sql = "INSERT INTO tb_instances (name) VALUES('{0}')".format(name)
            #print(sql)
            cursor.execute(sql)   
        
        classId = getClassIdFromName(cursor, typeName)
        sql = "UPDATE tb_instances SET classId = {0}, configName = '{1}', isExtension = {2} " \
              "WHERE name = '{3}'".format(classId, configName, isExtension, name)
        cursor.execute(sql)
        
    ################################################################################################  
    # insert files
    # remove instance files only
    sql = "DELETE FROM tb_files WHERE kind = 'instanceFile'"
    cursor.execute(sql)
    for fileName in classFileList:
        sql = "INSERT INTO tb_files (kind, fileName) VALUES('{0}', '{1}')".format('classFile', fileName)
        cursor.execute(sql)
    
    for fileName in instanceFileList:
        sql = "INSERT INTO tb_files (kind, fileName) VALUES('{0}', '{1}')".format('instanceFile', fileName)
        cursor.execute(sql)
      
    
    ################################################################################################
    # Print database
    ################################################################################################
    
    if 0:          
        sql = "SELECT * FROM tb_classes"
        cursor.execute(sql)
        for item in cursor:
            print(item)
        print("")
            
        sql = "SELECT * FROM tb_attributes"
        cursor.execute(sql)
        for item in cursor:
            print(item)
        print("")
            
        sql = "SELECT * FROM tb_instances"
        cursor.execute(sql)
        for item in cursor:
            print(item)
        print("")
            
        sql = "SELECT * FROM tb_files"
        cursor.execute(sql)
        for item in cursor:
            print(item)
    
    connection.commit()
    connection.close()
    