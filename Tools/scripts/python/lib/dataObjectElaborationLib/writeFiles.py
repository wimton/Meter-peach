####################################################################################################
#
#  Elaborates Marvin Persistent Object Code
#
#  Part 2 Writing C++ files based on Sqlite3 database.
#
####################################################################################################


import sqlite3
import os
import re

instanceOffset = 10000

def listSearch(seq, item):
    for e in seq:
        if e == item:
            return 1
    return None

def bitSize(input):
    numbBits = 0
    while input > 0:
        numbBits += 1
        input >>= 1
    return numbBits
        

####################################################################################################

def getStorageClassEnum(storageClass):
    return "MOS_DO_STORAGE_CLASS_" + storageClass
    
####################################################################################################

def getStorageBase(storageClass, className, name):
    global configBuildType
    if storageClass == "NO_STORAGE":
        return "NULL"
    elif storageClass == "PERSISTENT":
        return "PERSISTENT_ADDR({1}.{2})".format(storageClass, className, name) 
    elif storageClass == "FAST_ACCESS_UPDATE_RAM_COPY":
        return "FastAccess_{0}.{1}.{2}".format(configBuildType, className, name) 
    elif storageClass == "VOLATILE":
        return "VolatileRam_{0}.{1}.{2}".format(configBuildType, className, name) 
    elif storageClass == "BACKUP_RAM":
        return "BackupRam_{0}.{1}.{2}".format(configBuildType, className, name) 
    else:
        return "{0}.{1}.{2}".format(storageClass, className, name)  
  
  
####################################################################################################

def getTypeEnum(type):
    re1 = re.compile("\[(.*)\]")
    m1 = re1.findall(type) 
    if (m1):
        return "MOS_Do_t::VALUE_TYPE_ARRAY_PTR"  
    else:
        return "MOS_Do_t::VALUE_TYPE_VOID_PTR"  

  
####################################################################################################

def getNameFromClassId(db, classId):
    cursor = db.cursor()
    sql = "SELECT name from tb_classes WHERE classId = '{0}'".format(classId)
    cursor.execute(sql)
    name, = cursor.fetchone()
    return name

####################################################################################################

def getParentIdFromClassId(db, classId):
    cursor = db.cursor()
    sql = "SELECT parentId from tb_classes WHERE classId = '{0}'".format(classId)
    cursor.execute(sql)
    parentId, = cursor.fetchone()
    return parentId

####################################################################################################

def getChildrenIdFromClassId(db, classId):
    cursor = db.cursor()
    returnList = []
    sql = "SELECT classId from tb_classes WHERE parentId = '{0}'".format(classId)
    cursor.execute(sql)
    for classId, in cursor:
        returnList.append(classId)
        for item in getChildrenIdFromClassId(db, classId):
            returnList.append(item)
    return returnList


####################################################################################################

def getAttributeMacro(db, classId, attrName):
    className = getNameFromClassId(db, classId)
    attributeMacro = "MOS_DO_ATTR__{0}_{1}".format(className[0:len(className)-2], attrName)
    return attributeMacro

####################################################################################################

def getNumbAttributesFromClassId(db, classId):
    cursor = db.cursor()
    sql = "SELECT attributeId from tb_attributes WHERE classId = '{0}'".format(classId)
    cursor.execute(sql)
    returnValue = 0
    for dummy in cursor:
        returnValue += 1
    return returnValue


####################################################################################################

def getNumOfParentAttributes(db, classId):
    returnValue = 0
    parentId = getParentIdFromClassId(db, classId)
    while parentId != 0:
        returnValue += getNumbAttributesFromClassId(db, parentId)
        parentIdNew = getParentIdFromClassId(db, parentId)
        if parentId != parentIdNew:
            parentId = parentIdNew
        else:
            print("Elaboration error: class '{0}' has no valid parent".format(
                                                                  getNameFromClassId(db, parentId)))
            exit(1)
            
    return returnValue

####################################################################################################

def prependParents(db, classId):
        # include the parents to the front
        parentList = []
        parentId = classId
        while parentId != 0:
            parentList.insert(0, parentId)
            parentIdNew = getParentIdFromClassId(db, parentId)
            if parentId != parentIdNew:
                parentId = parentIdNew
            else:
                print("Elaboration error: class '{0}' has no valid parent".format(
                                                                  getNameFromClassId(db, parentId)))
                exit(1)  
        return parentList

####################################################################################################

def fileIsDifferent(file1, file2):
    try:
        f1 = open(file1, 'r')  
    except:
        # file t does not exist
        return 1
    f2 = open(file2, 'r')
    if f1.read() == f2.read():
        return 0
    return 1

####################################################################################################
   
def writeFile2(kind, fileName, db):
    if kind == "writeAttributeIds":
        writeAttributeIds(fileName, db)
    elif kind == "writeInstances":
        writeInstances(fileName, db)
    elif kind == "writeInstIds":
        writeInstIds(fileName, db)
    elif kind == "writeInstTypes":
        writeInstTypes(fileName, db)    
    elif kind == "writeConfiguration":
        writeConfiguration(fileName, db)  
    elif kind == "writeInstanceGetters":
        writeInstanceGetters(fileName, db)             
              

####################################################################################################
            
def writeFile(kind, fileName):
    global dbFile
    
    db = sqlite3.connect(dbFile)
    cursor = db.cursor()
    
    sql = "BEGIN EXCLUSIVE TRANSACTION"
    cursor.execute(sql)

    writeFile2(kind, fileName + '.tmp', db)
    if fileIsDifferent(fileName, fileName + '.tmp'):
        # the new file is different than the old one so write it with the official name
        writeFile2(kind, fileName, db)
    # remove the .tmp file
    os.remove(fileName + '.tmp')
    
    db.commit()
    db.close()
    
    
    
    
####################################################################################################

def writeStorageStruct(db, f, structName, storagePattern):
    global isExtension
    cursor = db.cursor()
    
    # create the class list and count the number of instances
    classList = []
    classInstanceCounter = {}
    sql = "SELECT classId FROM tb_instances WHERE isExtension = {0} ORDER BY instanceId".format(isExtension)    
    cursor.execute(sql)
    for classId, in cursor:
        
        if not listSearch(classList, classId):
            classList.append(classId)
            classInstanceCounter[classId] = 0
        classInstanceCounter[classId] += 1
     
    # Write out the struct
    print("", file=f)
    print("struct {0}".format(structName), file=f)
    print("{", file=f)
    
    if (storagePattern == 'BACKUP_RAM'):
        print("   U32_t beginFlag;", file=f)
             
    for classId in classList:
        itemList = []
        for id in prependParents(db, classId):
            sql = "SELECT name, type FROM tb_attributes " \
                  "WHERE classId = {0} AND storageClass LIKE '{1}' ORDER BY attributeId".format(id,
                                                                                     storagePattern)
            cursor.execute(sql)
            for item in cursor:
                itemList.append(item)
        
        if len(itemList) > 0:        
            className = getNameFromClassId(db, classId)  
            print("   struct {0}".format(className[0:len(className)-1]), file=f)
            print("   {", file=f)
            
            for name, type in itemList:
                newType = type
                newIndex = "" 
                re0 = re.compile("(.*)\[(.*)\]")
                m = re0.findall(type) 
                if (m):
                    #print(m)
                    newType = m[0][0]
                    newIndex = "[" + m[0][1] + "]" 
                    
                print("      {0} {1}[{2}]{3}; ".format(newType, name, classInstanceCounter[classId],
                                                       newIndex),file=f)       
            print("   }", end = " ", file=f)
            print("{0};".format(className[0:len(className)-2]), file=f)
            
    if (storagePattern == 'BACKUP_RAM'):
        print("   U32_t statusFlag;", file=f)
        
    print("};", file=f)


####################################################################################################
#
#
#  Writing Attribute ID file
#
#
####################################################################################################

def writeAttributeIds(fileName, db):
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Attribute IDs                                                      */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#ifndef MOS_DO_ATTR_HPP
#define MOS_DO_ATTR_HPP

////////////////////////////////////////////////////////////////////////////////////////////////////
enum MOS_DO_ATTR_t
{
   MOS_DO_ATTR__NotValid                                                       = 0,"""
    
    
    footer = """
////////////////////////////////////////////////////////////////////////////////////////////////////

#endif /* MOS_DO_ATTR_HPP */

"""
      
    f = open(fileName, 'w')
    
    print(header, file=f)
    
    # pass 1 official attribute names
    sql = "SELECT attributeId, name, classId, type FROM tb_attributes"
    cursor.execute(sql)
    
    for attributeId, name, classId, type in cursor:
        attributeMacro = getAttributeMacro(db, classId, name)
        print("   {0:75s} = {1}, /* {2} */".format(attributeMacro, attributeId, type), file=f)
    print("};", file=f)  
    print("\n\n", file=f)  
      
    # pass 2 name aliases for inherited attributes   
    print("// Alias definitions for children", file=f)  
    print("\n", file=f)  
    sql = "SELECT attributeId, name, classId, type FROM tb_attributes"
    cursor.execute(sql)
    
    for attributeId, name, classId, type in cursor:   
        printSeperator = 0  
        for childId in getChildrenIdFromClassId(db, classId):
            attributeMacro = getAttributeMacro(db, classId, name)
            aliasMacro = getAttributeMacro(db, childId, name)
            print("#define  {0:70s} {1}".format(aliasMacro, attributeMacro), file=f)
            printSeperator = 1
        if (printSeperator == 1):
            print("", file=f) 
        
    print(footer, file=f)
    f.close()
    
    
####################################################################################################
#
#
#  Writing Instances file
#
#
####################################################################################################

def writeInstances(fileName, db):
    global isExtension
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Instances                                                          */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#ifndef MOS_DO_INST_HPP
#define MOS_DO_INST_HPP

#include "MOS_Do_InstTypes.hpp"
"""
    
    
    footer = """
   
#endif /* MOS_DO_INST_HPP */

"""

    spacer = """
    
////////////////////////////////////////////////////////////////////////////////////////////////////

"""

    global instanceOffset
    f = open(fileName, 'w')
    
    print(header, file=f)
    
    sql = "SELECT fileName FROM tb_files WHERE kind = 'classFile'"
    cursor.execute(sql)
    for item in cursor:
        fileName, = item
        print("#include \"{0}\"".format(os.path.basename(fileName)), file=f)
     
    print(spacer, file=f)

    sql = "SELECT name, classId FROM tb_instances"
    cursor.execute(sql)           
    for item in cursor:
        name, classId = item
        print("extern class {0} {1};".format(getNameFromClassId(db, classId), name), file=f)
                
    print(spacer, file=f)
       
    print("", file=f)
    stencil = "bool MOS_Do_getInstanceByInstanceId(MOS_DO_INST_t id, {0} * & instance_p_r);"
    sql = "SELECT name FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name), file=f)  
        
    print("", file=f)
    stencil = "bool MOS_Do_getFirstInstanceByType(MOS_DO_TYPE_t type, {0} * & instance_p_r);"
    sql = "SELECT name FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name), file=f) 
        
    print("", file=f)
    stencil = "bool MOS_Do_getNextInstanceByType({0} * & instance_p_r);"
    sql = "SELECT name FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name), file=f) 
            
    
    print(footer, file=f)
    f.close()

####################################################################################################
#
#
#  Writing Instance ID file
#
#
####################################################################################################

def writeInstIds(fileName, db):
    global isExtension
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Instance IDs                                                       */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#ifndef MOS_DO_INST_ID_HPP
#define MOS_DO_INST_ID_HPP

"""
    
    
    footer = """
   
#endif /* MOS_DO_INST_ID_HPP */

"""

    global instanceOffset
    f = open(fileName, 'w')
    
    print(header, file=f)
    
    txt = """
enum MOS_DO_INST_t
{
   MOS_DO_INST__NotInUse = 0,
"""
    print(txt, file=f)
    
    sql = "SELECT instanceId, name FROM tb_instances"
    cursor.execute(sql)
    for item in cursor:
        instanceId, name = item
        fullName = "MOS_DO_INST__{0}".format(name)
        print("   {0:50s} = {1},".format(fullName, instanceId  +instanceOffset), file=f)
    
    print("};", file=f)
                   
    print(footer, file=f)
    f.close()



####################################################################################################
#
#
#  Writing Instance Types file
#
#
####################################################################################################

def writeInstTypes(fileName, db):
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Instance Types                                                     */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#ifndef MOS_DO_INST_TYPES_HPP
#define MOS_DO_INST_TYPES_HPP

"""
    
    
    footer = """
   
#endif /* MOS_DO_INST_TYPES_HPP */

"""

    global instanceOffset
    f = open(fileName, 'w')
    
    print(header, file=f)
    
    txt = """
enum MOS_DO_TYPE_t
{
   MOS_DO_TYPE__NotValid                         = 0,"""
    print(txt, file=f)
       
    sql = "SELECT classId,name FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    for classId,name in cursor:
        fullName = "MOS_DO_TYPE__{0}".format(name)
        print("   {0:50s} = {1},".format(fullName[0:len(fullName)-2], classId+1000), file=f)
        
    print("};", file=f)
                   
    print(footer, file=f)
    f.close()



####################################################################################################
#
#
#  Writing configuration file
#
#
####################################################################################################

def writeConfiguration(fileName, db):
    global isExtension
    global configBuildType
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Configuration                                                      */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#include <stddef.h>
#include "MOS_Do_Config.hpp"
#include "Cfg_ExternalMemoryDriver.h"
#include "MOS_Assert.h"

// -------------------------------------------------------------------------------------------------

"""

    spacer = """
    
////////////////////////////////////////////////////////////////////////////////////////////////////

"""

    code = """

static U8_t * const FastAccessExternalBase_{0} = reinterpret_cast<U8_t *>(PERSISTENT_PARAM_START_{0});
static U8_t * const PersistentExternalBase_{0} = FastAccessExternalBase_{0} + sizeof(FastAccess_t);


#define PERSISTENT_ADDR(field)  (PersistentExternalBase_{0} + offsetof(Persistent_t, field))
                                                                   
void rangeCheck{0}(void)
{1}
  MOS_ASSERT_STATIC((PERSISTENT_PARAM_END_{0} - PERSISTENT_PARAM_START_{0} + 1) 
                     >= (sizeof(FastAccess_t) + sizeof(Persistent_t)) );
{2}

//--------------------------------------------------------------------------------------------------
// Store configuration in ROM
//--------------------------------------------------------------------------------------------------

""".format(configBuildType, "{", "}")

    global instanceOffset
    f = open(fileName, 'w')
    
    print(header, file=f)
    
    sql = "SELECT fileName FROM tb_files WHERE kind = 'classFile'"
    cursor.execute(sql)
    for item in cursor:
        fileName, = item
        print("#include \"{0}\"".format(os.path.basename(fileName)), file=f)
     
    print(spacer, file=f)
    
    #-----------------------------------------------------------------------------------------------
        
    sql = "SELECT instanceId, name, configName, classId FROM tb_instances WHERE isExtension = {0}".format(isExtension)    
    cursor.execute(sql)
    for item in cursor:
        instanceId, name, configName, classId = item
        fullName = "{0}::Config_t".format(getNameFromClassId(db, classId))
        print("extern const {0:45s} {1};".format(fullName, configName), file=f)
     
    print(spacer, file=f)



    #-----------------------------------------------------------------------------------------------
    #  Write storage elements
    
    txt = """
//--------------------------------------------------------------------------------------------------
// Allocate Storage
//--------------------------------------------------------------------------------------------------
"""

    print(txt, file=f) 

    writeStorageStruct(db, f, "VolatileRam_t", 'VOLATILE')
    print("static VolatileRam_t VolatileRam_{0};".format(configBuildType), file=f)
     
    writeStorageStruct(db, f, "FastAccess_t", 'FAST_ACCESS%')
    print("static FastAccess_t FastAccess_{0};".format(configBuildType), file=f)
    
    writeStorageStruct(db, f, "BackupRam_t", 'BACKUP_RAM')
    print("static BackupRam_t BackupRam_{0};".format(configBuildType), file=f)

    writeStorageStruct(db, f, "Persistent_t", 'PERSISTENT')  
 
    print(code, file=f)      
    
    
    #-----------------------------------------------------------------------------------------------
    #  Write persistent object instance list
    
    numbMutexes = 0
    classList = []
    doTypeIdxMap = {}
    doTypeIdxMap[0] = -1
    instanceNumberMap = {}
    classNumbOfAttributes = {}
    classAttrIndexBase = {}
    
    index = 0
    sql = "SELECT classId FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    for classId, in cursor:
        doTypeIdxMap[classId] = index;        # give this class a type index
        classNumbOfAttributes[classId] = -1;  # reset number of attributes
        classAttrIndexBase[classId] = -1;     # reset attribute index base
        index += 1
    
    sql = "SELECT instanceId, name, configName, classId FROM tb_instances WHERE isExtension = {0}".format(isExtension)    
    cursor.execute(sql)
    instanceListSize = 0
    for dummy in cursor:
        instanceListSize += 1
        
    txt = """
//--------------------------------------------------------------------------------------------------
// Persistent object list
//--------------------------------------------------------------------------------------------------
static const MOS_Do_Config_InstanceEntry_t InstanceList_{0}[{1}] =
{2}
   //instanceUid;
   //   |                                                             instanceNumber;
   //   |                                                             |    doTypeIdx;
   //   |                                                             |    |    mutexIndex;
   //   |                                                             |    |    |   do_p
   //   |                                                             |    |    |   |                                        config_p
   //   |                                                             |    |    |   |                                        |""".format(configBuildType, instanceListSize, "{")

    print(txt, file=f)   
    
    cursor.execute(sql)
    for item in cursor:
        instanceId, name, configName, classId = item
        
        if not listSearch(classList, classId):
            classList.append(classId)       # this list is used to create the Attribute storage list
            instanceNumberMap[classId] = 0   # this is the first instance of this class
        
        uidName = "MOS_DO_INST__{0}".format(name)
        print("   {", end = " ", file=f)
        print("/*{0:6d} */ {1:<50s} {2:3d}, {3:3d}, {4:3d}, {5:<40s} {6:<45s}".format(
                            instanceId + instanceOffset,     # instanceUid as comment
                            uidName + ",",                   # instanceUid
                            instanceNumberMap[classId],      # instanceNumber
                            doTypeIdxMap[classId],           # doTypeIdx
                            numbMutexes,                     # mutexIndex
                            "&" + name + ",",                # do_p
                            "&" + configName + "," )         # config_p
                            , end = " ", file=f)
        print("},", file=f)
        numbMutexes += 1
        instanceNumberMap[classId] += 1
    
    print ("};", file=f) 
    
    print ("", file=f) 
    print ("//--------------------------------------------------------------------------------------------------", file=f) 
    print ("// Allocate Mutexes", file=f) 
    print ("//--------------------------------------------------------------------------------------------------", file=f) 
    print ("static MOS_OoMutex_t RwLock_{0}[{1}];".format(configBuildType, numbMutexes), file=f) 
   
    
    
    #-----------------------------------------------------------------------------------------------
    #  Write attribute descriptor list
    
    sql = "SELECT attributeId, name, classId, type, storageClass FROM tb_attributes ORDER BY attributeId"
    cursor.execute(sql)
    attrDescrSize = 0
    for dummy in cursor:
        attrDescrSize += 1
        
    txt = """
//--------------------------------------------------------------------------------------------------
// Attribute descriptor list
//--------------------------------------------------------------------------------------------------
#ifdef MOS_AUTO_TYPE_ENABLE_CHECKS
#define TYPE_SPEC(type) sizeof(type), typeid(type).name(), typeid(const type).name()
#else
#define TYPE_SPEC(type) sizeof(type)
#endif
static const MOS_Do_Config_AttributeDescr_t AttrDescr_{0}[{1}] =
{2}
   //        attrDescrUid;
   //        |                                                                                  attrIdx
   //        |                                                                                  |   attrType;
   //        |                                                                                  |   |                               attrSize;
   //        |                                                                                  |   |                               |                                                                                         StorageClass;
   //        |                                                                                  |   |                               |                                                                                         |""".format(configBuildType, attrDescrSize, "{")
   
    print(txt, file=f)   

    cursor.execute(sql)
    AttrIdxDict = {}
    descrIndex = 0
    descrIndexMap = {}
    for item in cursor:
        attributeId, name, classId, type, storageClass = item
        storageClassTypeEnum = getStorageClassEnum(storageClass)
        typeEnum = getTypeEnum(type)
        
        re0 = re.compile("(.*)\[(.*)\]")
        m0 = re0.findall(type) 
        if (m0):
            itemType = m0[0][0]
        else:
            itemType = type       
        
        if not listSearch(AttrIdxDict.keys(), classId):
            # class not yet in the dictionary -> create an entry
            AttrIdxDict[classId] = getNumOfParentAttributes(db, classId)
            
        uidName = getAttributeMacro(db, classId, name)
        print("   {", end = " ", file=f)
        print("/*{0:3d} {1:3d} */ {2:75s} {3:3d}, {4:32s} TYPE_SPEC({5:40s}), TYPE_SPEC({6:35s}), {7:50s}".format(
                            descrIndex,                 # row number of the table
                            attributeId,                # AttrDescrUid as comment
                            uidName + ",",              # AttrDescrUid
                            AttrIdxDict[classId],       # AttrIdx
                            typeEnum + ",",             # AttrType
                            type,                       # AttrSize
                            itemType,                   # ItemSize
                            storageClassTypeEnum + ",") # StorageClass
                            , end = " ", file=f)
        print("},", file=f)
        AttrIdxDict[classId] += 1
        descrIndexMap[attributeId] = descrIndex
        descrIndex += 1
         
    print ("};", file=f) 


    #-----------------------------------------------------------------------------------------------
    #  Write attribute storage list
    
    txt = """
//--------------------------------------------------------------------------------------------------
// Attribute storage list
//--------------------------------------------------------------------------------------------------
static const MOS_Do_Config_Attribute_t AttributeStorageList_{0}[] =
{1}
   //           descrIndex;
   //           |             storageBase;
   //           |             |""".format(configBuildType, "{")
   
    print(txt, file=f)   
    storageListIndex = 0

    for classId in classList:
        classAttrIndexBase[classId] = storageListIndex
        classNumbOfAttributes[classId] = 0
        className = getNameFromClassId(db, classId)  
        print("", file=f)
        print("                   // Instance {0}".format(className), file=f)
        
        for id in prependParents(db, classId):
            sql = "SELECT attributeId, name, storageClass FROM tb_attributes " \
                  "WHERE classId = {0} ORDER BY attributeId".format(id)
            cursor.execute(sql)
            for item in cursor:
                attributeId, name, storageClass = item
                storageBase = getStorageBase(storageClass, className[0:len(className)-2], name)     
                print("   {", end = " ", file=f)
                print("/*{0:3d} */ {1:3d}, {2:77s}".format(
                                    storageListIndex,            # row number of the table
                                    descrIndexMap[attributeId],  # DescrIndex
                                    storageBase)                 # StorageBase
                                    , end = " ", file=f)
                print("},", file=f)
                storageListIndex += 1
                classNumbOfAttributes[classId] += 1
         
    print ("};", file=f) 


    #-----------------------------------------------------------------------------------------------
    #  Write instance type list
    
    sql = "SELECT classId FROM tb_classes ORDER BY classId"
    cursor.execute(sql)
    typeListSize = 0
    for dummy in cursor:
        typeListSize += 1
        
    txt = """
//--------------------------------------------------------------------------------------------------
// Instance type list
//--------------------------------------------------------------------------------------------------
static const MOS_Do_Config_TypeEntry_t TypeList_{0}[{1}] =
{2}
   //          typeId
   //          |                                                               numbOfAttributes;
   //          |                                                               |    attrIndexBase;
   //          |                                                               |    |    parentIndex
   //          |                                                               |    |    |""".format(configBuildType, typeListSize, "{")
   
    print(txt, file=f)   
    
    index = 0
    cursor.execute(sql)
    for classId, in cursor:
        typeId = "MOS_DO_TYPE__{0}".format(getNameFromClassId(db, classId))
        print("   {", end = " ", file=f)
        print("/*{0:3d} */  {1:60s}  {2:3d}, {3:3d}, {4:3d}".format(
                                    index,                           # row number of the table
                                    typeId[0:len(typeId)-2] + ",",   # typeId
                                    classNumbOfAttributes[classId],  # NumbOfAttributes
                                    classAttrIndexBase[classId],     # AttrIndexBase
                                    doTypeIdxMap[getParentIdFromClassId(db, classId)]) #parentIndex
                                    , end = " ", file=f)
        print("},", file=f)
        index += 1
        
    print ("};", file=f) 
 
    print(spacer, file=f)

 
    txt = """  
const MOS_Do_Config_t MOS_Do_Config_{0} =  
{1}
   AttrDescr_{0},                // MOS_Do_Config_AttributeDescr_t * AttrDescr;
   {3},                          // U16_t                            AttributeDescrListSize;
   {4},                          // U16_t                            AttributeDescrListSizeBin;
   AttributeStorageList_{0},     // MOS_Do_Config_Attribute_t *      AttributeStorageList;
   InstanceList_{0},             // MOS_Do_Config_InstanceEntry_t *  InstanceList;
   {5},                          // U16_t                            InstanceListSize;
   {6},                          // U16_t                            InstanceListSizeBin;
   TypeList_{0},                 // MOS_Do_Config_TypeEntry_t *      TypeList;
   {7},                          // U16_t                            TypeListSize;
   {8},                          // U16_t                            TypeListSizeBin;
   RwLock_{0},                   // MOS_OoMutex_t *                  RwLock;
   ARRAY_SIZE(RwLock_{0}),       // U16_t                            RwLockListSize;

   reinterpret_cast<U8_t *>(&VolatileRam_{0}),          // U8_t * VolatileRamBase;
   sizeof(VolatileRam_{0}),                             // U16_t  VolatileRamSize;
   reinterpret_cast<U8_t *>(&FastAccess_{0}),           // U8_t * FastAccessBase;
   sizeof(FastAccess_{0}),                              // U16_t  FastAccessSize;
   FastAccessExternalBase_{0},                          // U8_t * FastAccessExternalBase;
   &BackupRam_{0}.beginFlag,                            // U32_t * BackupRamBegin_p;
   &BackupRam_{0}.statusFlag,                           // U32_t * BackupRamStatus_p;
{2};

""".format(configBuildType, "{", "}", attrDescrSize, 2**bitSize(attrDescrSize),
                                      instanceListSize, 2**bitSize(instanceListSize),
                                      typeListSize, 2**bitSize(typeListSize))

    print(txt, file=f)   
    f.close()
    
    
    
    
    
    
####################################################################################################
#
#
#  Writing instance getter file
#
#
####################################################################################################

def writeInstanceGetters(fileName, db):
    global isExtension
    
    cursor = db.cursor()
    header = """
/* ---------------------------------------------------------------------------------------------- */
/*                           (C) Copyright Landis + Gyr, 2007-2011                                */
/*                                                                                                */
/* This source code and any compilation or derivative thereof is protected by intellectual        */
/* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     */
/* and is confidential in nature.                                                                 */
/*                                                                                                */
/* Under no circumstances the content must not be copied, disseminated, amended, made accessible  */
/* to third parties nor used in any other way without explicit written consent of the Landis+Gyr. */
/* ---------------------------------------------------------------------------------------------- */
/*                                                                                                */
/*                                                                                                */
/*  Purpose: Persistent Object Instance getters                                                   */
/*                                                                                                */
/*  ATTENTION: This is a generated file all modification will be overwritten during the           */
/*             elaboration stage                                                                  */ 
/**************************************************************************************************/
    
#include "MOS_Do_Instances.hpp"
#include "MOS_DoManager.hpp"

"""
    f = open(fileName, 'w')
    print(header, file=f)       
    
    stencil = """ 
bool MOS_Do_getInstanceByInstanceId(MOS_DO_INST_t id, {0}_t * & instance_p_r)
{1}
   MOS_Do_t * do_p;
   if (MOS_DoManager_t::getInstanceByInstanceId(MOS_DO_TYPE__{0}, id, do_p))
   {1}
      instance_p_r = static_cast<{0}_t *>(do_p);
      return true;
   {2}
   instance_p_r = 0;
   return false;
{2}"""
    
    sql = "SELECT name FROM tb_classes WHERE isExtension = {0} ORDER BY classId".format(isExtension)
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name[0:len(name)-2], "{", "}"), file=f)
    
    
    stencil = """ 
bool MOS_Do_getFirstInstanceByType(MOS_DO_TYPE_t type, {0}_t * & instance_p_r)
{1}
   MOS_Do_t * do_p;
   if (MOS_DoManager_t::getFirstInstanceByType(MOS_DO_TYPE__{0}, type, do_p))
   {1}
      instance_p_r = static_cast<{0}_t *>(do_p);
      return true;
   {2}
   instance_p_r = 0;
   return false;
{2}"""
    

    sql = "SELECT name FROM tb_classes WHERE isExtension = {0} ORDER BY classId".format(isExtension)
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name[0:len(name)-2], "{", "}"), file=f)
        
        
    stencil = """ 
bool MOS_Do_getNextInstanceByType({0}_t * & instance_p_r)
{1}
   MOS_Do_t * do_p = instance_p_r;
   if (MOS_DoManager_t::getNextInstanceByType(do_p))
   {1}
      instance_p_r = static_cast<{0}_t *>(do_p);
      return true;
   {2}
   instance_p_r = 0;
   return false;
{2}"""
    
    sql = "SELECT name FROM tb_classes WHERE isExtension = {0} ORDER BY classId".format(isExtension)
    cursor.execute(sql)
    for name, in cursor:
        print(stencil.format(name[0:len(name)-2], "{", "}"), file=f)
       

    f.close()
     


####################################################################################################
#
#
#  MAIN
#
#
####################################################################################################

def main(_verbose, dirname, _dbName, _isExtension):
    global verbose
    global isExtension
    global dbFile 
    global configBuildType
     
    dbFile = _dbName 
    if(_isExtension == 1):
        configBuildType = 'Extension'
    else:
        configBuildType = 'Core'
    isExtension = _isExtension   
    verbose = _verbose
              
    writeFile("writeAttributeIds",    dirname + '/inc/MOS_Do_Attr.hpp')
    writeFile("writeInstances",       dirname + '/inc/MOS_Do_Instances.hpp')
    writeFile("writeInstIds",         dirname + '/inc/MOS_Do_InstId.hpp')
    writeFile("writeInstTypes",       dirname + '/inc/MOS_Do_InstTypes.hpp')
    writeFile("writeConfiguration",   dirname + '/src/MOS_Do_Config.cpp')
    writeFile("writeInstanceGetters", dirname + '/src/MOS_Do_Instances.cpp')
      
