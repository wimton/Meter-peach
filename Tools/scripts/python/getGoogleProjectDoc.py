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

class GoogleProjectDownloader(object):
    def __init__(self):
        self.__parseArguments()
        self.__initFiles()

    def getFiles(self):
        self.__createTargetDir()
        self.__downloadFiles()
        self.__extractContents()

    def __parseArguments(self):
        import argparse
        import os.path

        parser = argparse.ArgumentParser()
        parser.add_argument('projectName', help='the name of the project as defined on code.google.com')
        parser.add_argument('-v','--version', type=float, help='the version of the project')
        parser.add_argument('-t','--targetDir', help='the directory where the files will be stored in', default='.')
        args = parser.parse_args()
        self.projectName = args.projectName
        self.__url = 'https://code.google.com/p/' + self.projectName + '/wiki'
        self.__version = args.version
        self.targetDir = os.path.join(os.path.abspath(args.targetDir),args.projectName)

    def __initFiles(self):
        versionPrefix = self.__composeVersionString()
        self.__files = [('{0}/{1}{2}'.format(self.__url,versionPrefix,file),'{0}.html'.format(file))
                        for file in ['AdvancedGuide','CheatSheet','CookBook','FAQ','ForDummies',
                                     'FrequentlyAskedQuestions', 'Primer','PumpManual','Samples','XcodeGuide'] ]

    def __createTargetDir(self):
        from os import chdir, mkdir, getcwd
        for dir in [self.targetDir,self.__composeVersionDir()]:
            try:
                mkdir(dir)
            except FileExistsError:
                pass
            chdir(dir)
 
    def __composeVersionString(self):
        if self.__version == None:
            return ''
        else:
            return 'V' + str(self.__version).replace('.','_') + '_'

    def __composeVersionDir(self):
        if self.__version == None:
            return currentVersionDirName
        else:
            return str(self.__version)

    def __downloadFiles(self):
        import urllib.error
        for file in self.__files:
            try:
                self.__downloadFile(file)
                print(file[0],'downloaded and stored.')
            except urllib.error.HTTPError:
                print(file[0],'not found. Ignoring file.')

    def __downloadFile(self,file):
        from urllib.request import urlretrieve
        urlretrieve(file[0],file[1])

    def __extractContents(self):
        for localFile in [file[1] for file in self.__files]:
            try:
                self.__writeFile(localFile, self.__readFile(localFile))
            except FileNotFoundError:
                pass

    def __readFile(self, localFileName):
        linesToSave = []
        file = open(localFileName,'r',encoding='utf-8')
        print('Extracting relevant content from',localFileName)
        parser = WikiExtractor()
        for line in file:
            parser.feed(line)
            if parser.isSaving:
                linesToSave.append(line)
                
        file.close()
        self.__contentTypeTag = parser.contentTypeTag
        return linesToSave

    def __writeFile(self, localFileName, linesToWrite):
        file = open(localFileName,'w',encoding='utf-8')
        file.write('<html><body>\n')
        file.write(self.__contentTypeTag)
        for line in linesToWrite:
            file.write(line)
        file.write('</body></html>\n')
        file.close()


from html.parser import HTMLParser

class WikiExtractor(HTMLParser):
    def __init__(self):
        super().__init__()
        self.isSaving = False
        self.__endSaving = False
        self.__divStackSize = 0
        self.contentTypeTag = ''

    def handle_starttag(self, tag, attrs):
        self.__handleEndSaving()

        if self.__isContentTypeInfo(tag, attrs):
            self.contentTypeTag = '<meta http-equiv="Content-Type" content="' + self.__getContentType(attrs) + '" />\n'
        elif self.__isStartTag(tag,attrs):
            self.isSaving = True
        
        self.__handleStartDiv(tag)

    def handle_endtag(self, tag):
        self.__handleEndSaving()
        self.__handleEndDiv(tag)

    def handle_startendtag(self, tag, attrs):
        self.__handleEndSaving()

    def __isStartTag(self, tag, attrs):
        if tag != 'div':
            return False
        classFound = False
        idFound = False
        for attrKey, attrValue in attrs:
            if attrKey == 'class':
                classFound = True
                if attrValue != 'vt':
                    return False
            elif attrKey == 'id':
                idFound = True
                if attrValue != 'wikimaincol':
                    return False
        return classFound and idFound

    def __isContentTypeInfo(self, tag, attrs):
        if tag != 'meta':
            return False
        
        for attrKey, attrValue in attrs:
            if attrKey == 'http-equiv':
                return attrValue == 'Content-Type'
        return False

    def __getContentType(self, attrs):
        for attrKey, attrValue in attrs:
            if attrKey == 'content':
                return attrValue

    def __handleStartDiv(self, tag):
        if tag == 'div' and self.isSaving:
            self.__divStackSize += 1

    def __handleEndDiv(self, tag):
        if tag != 'div' or not self.isSaving:
            return

        self.__divStackSize -= 1

        if self.__divStackSize == 0:
            self.__endSaving = True

    def __handleEndSaving(self):
        if self.__endSaving:
            self.isSaving = False
            self.__endSaving = False

class IndexCreator(object):
    def __init__(self, targetDir, projectName):
        from os import chdir
        self.__targetDir = targetDir
        self.__projectName = projectName
        chdir(self.__targetDir)

    def createIndex(self):
        with open('index.html', 'w') as outputFile:
            global write
            write = lambda *args : print(*args,file=outputFile,sep='')
            self.__gatherAvailableFiles()
            self.__createFile()

    def __gatherAvailableFiles(self):
        from os import listdir, path
        self.__files = {}
        # gather the names of all folders in the current directory. these are the available versions
        for version in [file for file in listdir(self.__targetDir) if path.isdir(path.join(self.__targetDir,file))]:
            versionDir = path.join(self.__targetDir,version)
            # for each version folder gather the relative paths to all contained files 
            self.__files[version] = [path.relpath(path.join(versionDir,file)) for file in listdir(versionDir) if path.isfile(path.join(versionDir,file)) ]

    def __createFile(self):
        write('<html>\n <body>\n  <h1>Documentation Overview For <i>',self.__projectName,'</i>:</h1>\n  <ul>')
        self.__writeCurrentVersion()
        self.__writeEnumeratedVersions()
        write('  </ul>\n </body>\n<html>')

    def __writeCurrentVersion(self):
        if currentVersionDirName in self.__files:
            self.__writeVersion(currentVersionDirName)
            del self.__files[currentVersionDirName]

    def __writeEnumeratedVersions(self):
        for version in sorted(self.__files, reverse=True):
            self.__writeVersion(version)

    def __writeVersion(self, version):
        from os import path
        write('   <li><h2>',version,'</h2>\n    <ul>')
        for file in sorted(self.__files[version]):
            write('     <li><a href="',file,'">',path.splitext(path.basename(file))[0],'</a></li>')
        write('    </ul>\n   </li>')


currentVersionDirName = 'current'

if __name__ == '__main__':
    downloader = GoogleProjectDownloader()
    print('Saving documenation to',downloader.targetDir)
    downloader.getFiles()
    print('Creating index file...')
    IndexCreator(downloader.targetDir,downloader.projectName).createIndex()
    print('done')
