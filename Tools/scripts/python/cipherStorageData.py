###############################################################################
#
#  encrypts the provided input file (AES-128 + CMAC checksum)
#
# arg 1: encryption key 32byte string (first 16Bytes is aesDataKey second 16Bytes is aesCmacKey)
# arg 2: target storage address - the address where the output file will stored
# arg 3: input file
# arg 4: output file
#
###############################################################################

import sys
import os
import struct
import hashlib
from Crypto.Cipher import AES
from Crypto.Hash import CMAC

###############################################################################
def getFileHeader(fileSize, sectorSize):
    data = b'';
    data += struct.pack("<i", 0x3700AA55)      # sentinel
    data += struct.pack("<i", fileSize)        # file size
    for a in range (sectorSize-8):             # padd sector with zeros
        data += b'\x00'
    return data

###############################################################################
def getIv(sectorNumber):
    data = b'';
    data += struct.pack("<i", sectorNumber)
    for a in range (16-4):                     # padd sector with zeros
        data += b'\x00'
    return data

###############################################################################
def checksumGet(key, iv, data):
    mac = CMAC.new(key, ciphermod=AES)
    mac.update(iv)
    mac.update(data)
    digest = mac.digest()
    return digest

###############################################################################
def paddingSet(data, padNum, padVal):
    for i in range(0, padNum):
        data += padVal
    return data

###############################################################################
def aesEncript(data, key, iv):
    aes = AES.new(key, AES.MODE_CBC, iv)
    return aes.encrypt(data)

###############################################################################
argNum = 4

if len(sys.argv) != (argNum + 1):
    print ('invalid parameter(s)')
    exit()

tatgetStorageSectorSize = 4096
checksumLen = 16
plainBinFileReadBlockSize = tatgetStorageSectorSize - checksumLen

inputFilePath = sys.argv[3]
outputFilePath = sys.argv[4]
inputFileSize = os.path.getsize(inputFilePath)
aesDataKey = bytes.fromhex(sys.argv[1][0:32])
aesCmacKey = bytes.fromhex(sys.argv[1][32:64])
storageAddress = int(sys.argv[2], 16)
storageSector = int(storageAddress / tatgetStorageSectorSize)

print ('input file:', inputFilePath)
print ('output file:', outputFilePath)
print ('AES encryption key:', aesDataKey)
print ('AES CMAC key:', aesCmacKey)
print ('target storage address:', storageAddress)
print ('target storage sector:', storageSector)

inputFile = open(inputFilePath, 'rb')
outputFile = open(outputFilePath, 'wb+')

inputFileHeader = getFileHeader(inputFileSize, plainBinFileReadBlockSize)
aesIv = getIv(storageSector)
encryptedInputFileHeader = aesEncript(inputFileHeader, aesDataKey, aesIv)
outputFileChunk = encryptedInputFileHeader + checksumGet(aesCmacKey, aesIv, encryptedInputFileHeader)
outputFile.write(outputFileChunk)
storageSector += 1

remainingRd = inputFileSize
while  remainingRd > 0:
    if remainingRd > plainBinFileReadBlockSize:
        rdLen = plainBinFileReadBlockSize
    else:
        rdLen = remainingRd

    inputFileChunk = inputFile.read(rdLen)
    remainingRd -= rdLen

    if rdLen < plainBinFileReadBlockSize:
        inputFileChunk = paddingSet(inputFileChunk, plainBinFileReadBlockSize - rdLen, b'\x00')

    aesIv = getIv(storageSector)
    encryptedInputFileChunk = aesEncript(inputFileChunk, aesDataKey, aesIv)
    outputFileChunk = encryptedInputFileChunk + checksumGet(aesCmacKey, aesIv, encryptedInputFileChunk)
    outputFile.write(outputFileChunk)
    storageSector += 1

outputFile.close()
inputFile.close()

print('file encryption DONE!')
