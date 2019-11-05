#* ---------------------------------------------------------------------------------------------- *#
#*                           (C) Copyright Landis + Gyr, 2007-2013                                *#
#*                                                                                                *#
#* This source code and any compilation or derivative thereof is protected by intellectual        *#
#* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     *#
#* and is confidential in nature.                                                                 *#
#*                                                                                                *#
#* Under no circumstances shall the content be copied, disseminated, amended or made accessible   *#
#* (in whole or in part) to third parties nor used in any other way without the prior written     *#
#* consent of Landis+Gyr.                                                                         *#
#* ---------------------------------------------------------------------------------------------- *#

from xlrd import open_workbook

class SpecFileException(BaseException):
    pass

class FileGenerator(object):
    def __init__(self):
        self.__imports = ['cosem.classes as classes']

    def writeCopyrightHeader(self):
        write('#* ---------------------------------------------------------------------------------------------- *#')
        write('#*                           (C) Copyright Landis + Gyr, 2007-2013                                *#')
        write('#*                                                                                                *#')
        write('#* This source code and any compilation or derivative thereof is protected by intellectual        *#')
        write('#* property rights (in particular copyright) and is the proprietary information of Landis+Gyr     *#')
        write('#* and is confidential in nature.                                                                 *#')
        write('#*                                                                                                *#')
        write('#* Under no circumstances shall the content be copied, disseminated, amended or made accessible   *#')
        write('#* (in whole or in part) to third parties nor used in any other way without the prior written     *#')
        write('#* consent of Landis+Gyr.                                                                         *#')
        write('#* ---------------------------------------------------------------------------------------------- *#')
        write()

    def writeImports(self):
        for i in self.__imports:
            write('import ',i)
        write()

    def writeTrees(self):
        mTree = ManagementTreeGenerator()
        cTree = ConsumerTreeGenerator()
        mTree.lastRow = cTree.titleRow - 1
        cTree.lastRow = specTable.nrows - 1

        mTree.writeTree()
        #cTree.writeTree()


class TreeGenerator(object):

    column_ID = 1
    column_AttributeType = 4
    column_DataType = 5
    column_ObjectID = column_defaultValue = 6

    def __init__(self,treeName):
        self.__treeName = treeName

    def writeTree(self):
        write('class ',self.__treeName,'(classes.logicalDevice):')
        write(classIndent,'def __init__(self, conn, logicalDevice):')
        write(defIndent,'s = (conn, logicalDevice)')
        write(defIndent,'super().__init__(conn, logicalDevice)')
        write(defIndent,'self.reset()')
        write()
        self.__writeReset()
        write()

    def __writeReset(self):
        write(classIndent,'def reset(self):')

        self.advanceCurrentRow()

        while self.currentRow < self.lastRow:
            self.writeConstruction()
            self.advanceCurrentRow()
            write()

        write()

        
    def findTitle(self,titleName):
        for row in range(specTable.nrows):
            if titleName == specTable.cell(row,TreeGenerator.column_ID).value:
                self.titleRow = self.lastRow = self.currentRow = row
                return
        raise SpecFileException('did not find title %s' % titleName)

    def advanceCurrentRow(self):
        for row in range(self.currentRow + 1, self.lastRow):
            if specTable.cell(row,TreeGenerator.column_ID).value:
                self.currentRow = row
                return

        self.currentRow = self.lastRow

    def writeConstruction(self):
        objectName = self.replaceInvalidChars(specTable.cell(self.currentRow,TreeGenerator.column_ID).value)
        canonicalObjectName = 'self.' + objectName
        objectID = specTable.cell(self.currentRow,TreeGenerator.column_ObjectID).value
        write(defIndent,canonicalObjectName,' = classes.data(s,"',objectID,'")')
        self.currentRow += 1
        while specTable.cell(self.currentRow,TreeGenerator.column_ID).value:
            self.writeAttribute(canonicalObjectName,self.currentRow)
            self.currentRow += 1

    def writeAttribute(self, objectName, row):
        if not self.isAttribute(row):
            return
        attrName, attrType, defaultValue = self.getAttributeData(row)
        if self.isValidDefaultValue(defaultValue) and attrName != 'attribute_logical_name':
            write(defIndent,objectName,'.',attrName,' = ',self.convertValue(defaultValue, attrType))

    def isValidDefaultValue(self, value):
        return value and value != '-' and value != 'tbd'

    def replaceInvalidChars(self,value):
        import re
        return re.sub('[\s\-\(\)&]','_',value)

    def convertValue(self, value, type):
        import re
        if type == 'boolean':
            if str(value) == '0' or str(value) == 'FALSE':
                return False
            elif str(value) == '1' or str(value) == 'TRUE':
                return True
            else:
                raise SpecFileException('Unknown format of boolean value')
        elif type[:12] == 'octet_string':
            if not self.isString(value):
                return "'" + value + "'"
        elif type[:5] == 'array' or type == 'scal_unit_type':
            return re.sub('[\{\[]','(',re.sub('[\}\]]',')',value))
        elif value == 'none':
            return None
        return value

    def isString(self,value):
        return value[0] == "'" and value[-1] == "'" or value[0] == '"' and value[-1] == '"'

    def isAttribute(self, row):
        return specTable.cell(row,TreeGenerator.column_AttributeType).value != 'm'

    def getAttributeData(self, row):
        return ('attribute_' + self.replaceInvalidChars(specTable.cell(row,TreeGenerator.column_ID).value),
                specTable.cell(row,TreeGenerator.column_DataType).value,
                specTable.cell(row,TreeGenerator.column_defaultValue).value)

    
class ManagementTreeGenerator(TreeGenerator):
    def __init__(self):
        super().__init__('ManagementTree')
        self.findTitle('Logical device management (GW)')




class ConsumerTreeGenerator(TreeGenerator):
    def __init__(self):
        super().__init__('ConsumerTree')
        self.findTitle('Logical device consumer 1')

def openSpecificationTable():
    inputFileName = 'C:\\Users\\extgubserm\\Documents\\Visual Studio 2010\\Projects\\DEV\\Documents\\Release_Notes\\SMGW_PoC_Object_Model.xlsx'
    return open_workbook(inputFileName).sheet_by_index(1)

specTable = openSpecificationTable()
    
classIndent = '    '
defIndent = 2 * classIndent

with open('new_objectTree.py', 'w') as outputFile:
    try:
        write = lambda *args : print(*args,file=outputFile,sep='')
        file = FileGenerator()
        file.writeCopyrightHeader()
        file.writeImports()
        file.writeTrees()
    except SpecFileException as e:
        print('Specification file is badly formatted:',e)

print()
#print('Sheet:',specTable.name)
#for row in range(specTable.nrows):
#    values = []
#    for col in range(specTable.ncols):
#        values.append(specTable.cell(row,col).value)
#    print(values,file=o)

print("object tree generated")

