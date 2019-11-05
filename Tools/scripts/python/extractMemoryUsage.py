###############################################################################
#
#  Extracts memory usage from IAR mapping file
#
###############################################################################


import re
import sys

base = {}
size = {}

#--------------------------------------------------------------------------------
#  determine which microcontroller
#--------------------------------------------------------------------------------
micro = "stm32"
if len(sys.argv) > 1:
    if sys.argv[1]: 
        if sys.argv[1] == "nxp":
            micro = "nxp"
        if sys.argv[1] == "rx":
            micro = "rx"
        if sys.argv[1] == "stm32F42":
            micro = "stm32F42"

buildNumberKey = ""
if len(sys.argv) > 2:
    if sys.argv[2]: 
        buildNumberKey = sys.argv[2]

xmlLocation = "obj"
if len(sys.argv) > 3:
    if sys.argv[3]: 
        xmlLocation = sys.argv[3]

vsProject = "VS Project XY"
if len(sys.argv) > 3:
    if sys.argv[4]: 
        vsProject = sys.argv[4]

f = open('obj/build.map', 'r')


for line in f:
    # looking for section base addresses
    # "P21", part 1 of 3:                              0x200
    grepSection = re.compile("^\"(.*)\".*: *(0x[0-9a-f]*)")
    m = grepSection.findall(line)
    if m:
        #print("'{0}' '{1}'  {2}".format(m[0][0], m[0][1], line))
        key = m[0][0]
        value = int(m[0][1],16) 
        if key in size.keys():
            size[key] += value
        else:
            size[key] = value
       
    # looking for section size   
    # "P2":  place in [from 0xc0000000 to 0xc00009eb] {   
    # "P3":  place in [from 0x68000000 to 0x6807ffff] 
    grepSection = re.compile("^\"(.*)\":.* (at|in) .*(0x[0-9a-f][0-9a-f])")   
    m = grepSection.findall(line)
    if m:
        #print("'{0}' '{1}'  {2}".format(m[0][0], m[0][2], line))
        base[m[0][0]] = m[0][2]
      
Ram  = 0
Ccmram = 0
Rom  = 0
Sram = 0
Sdram = 0 
Flash = 0 
Unknown = 0

Sum  = 0

if micro == "nxp":
    for key in base.keys():
        sizeInt = int(size[key])
        #print("{0} {1} ".format(base[key], sizeInt))
        if base[key] == "0x1a":
            Rom += sizeInt
        elif base[key] == "0x1b":
            Rom += sizeInt
        elif base[key] == "0x10":
            Ram += sizeInt
        elif base[key] == "0x20":
            Ram += sizeInt
        elif base[key] == "0x00":
            Flash += sizeInt
        elif base[key] == "0x28":
            Sdram += sizeInt
        else:
            print("{0} {1} ".format(base[key], sizeInt))
            Unknown += sizeInt
        Sum += sizeInt
elif micro == "rx":
    for key in base.keys():
        sizeInt = int(size[key])
        #print("{0} {1} ".format(base[key], sizeInt))
        if base[key] == "0xff":
            Rom += sizeInt
        elif base[key] == "0x00":
            Ram += sizeInt
        elif base[key] == "0x08":
            Sdram += sizeInt
        else:
            print("{0} {1} ".format(base[key], sizeInt))
            Unknown += sizeInt
        Sum += sizeInt
elif micro == "stm32F42":
    for key in base.keys():
        sizeInt = int(size[key])
        #print("{0} {1} ".format(base[key], sizeInt))
        if base[key] == "0x08":
            Rom += sizeInt
        elif base[key] == "0x20":
            Ram += sizeInt
        elif base[key] == "0x10":
            Ccmram += sizeInt
        elif base[key] == "0xc0":
            Sdram += sizeInt
        else:
            print("{0} {1} ".format(base[key], sizeInt))
            Unknown += sizeInt
        Sum += sizeInt
else: # stm
    for key in base.keys():
        sizeInt = int(size[key])
        #print("{0} {1} ".format(base[key], sizeInt))
        if base[key] == "0x08":
            Rom += sizeInt
        elif base[key] == "0x20":
            Ram += sizeInt
        elif base[key] == "0xc0":
            Flash += sizeInt
        elif base[key] == "0x68":
            Sram += sizeInt
        else:
            print("{0} {1} ".format(base[key], sizeInt))
            Unknown += sizeInt
        Sum += sizeInt

print()
print("Memory usage ({0}):".format(micro))
print("RAM:      {0:8d} bytes".format(Ram))
print("CCMRAM:   {0:8d} bytes".format(Ccmram))
print("ROM:      {0:8d} bytes".format(Rom))
print("SRAM:     {0:8d} bytes".format(Sram))
print("SDRAM:    {0:8d} bytes".format(Sdram))
print("FLASH:    {0:8d} bytes".format(Flash))
print("Unknown:  {0:8d} bytes".format(Unknown))
print("Total:    {0:8d} bytes".format(Sum))

f = open('obj/mem_usage.txt', 'w')
print("RAM,CCMRAM,ROM,SRAM,SDRAM,FLASH,Unknown,Total", file=f)
print("{},{},{},{},{},{},{},{}".format(Ram, Ccmram, Rom, Sram, Sdram, Flash, Unknown, Sum), file=f)
f.close()

template1 = """<?xml version="1.0" encoding="utf-8"?>
<BuildResults BuildNumberKey="{}" xmlns="http://tempuri.org/GenericInputDataLoader.xsd">"""

template2 = """
    <MeasuresIncluded MeasureTypeKey="RAM" Name="RAM internal 1" FormatString="0 bytes" Color="Blue" />
    <MeasuresIncluded MeasureTypeKey="CCMRAM" Name="RAM internal 2" FormatString="0 bytes" Color="Aqua" />
    <MeasuresIncluded MeasureTypeKey="ROM" Name="ROM" FormatString="0 bytes" Color="Green" />
    <MeasuresIncluded MeasureTypeKey="SRAM" Name="SRAM" FormatString="0 bytes" Color="Yellow" />
    <MeasuresIncluded MeasureTypeKey="SDRAM" Name="SDRAM" FormatString="0 bytes" Color="Orange" />
    <MeasuresIncluded MeasureTypeKey="FLASH" Name="FLASH" FormatString="0 bytes" Color="Lime" />
    <MeasuresIncluded MeasureTypeKey="UNKNOWN" Name="Unknown" FormatString="0 bytes" Color="Grey" />
    <MeasuresIncluded MeasureTypeKey="TOTAL" Name="Total Memory used" FormatString="0 bytes" Color="Red" />
    <ReportResults ResultSourceKey="Debug|IarArm">
      <Report ReportTypeKey="MemoryUsage">
        <Name>Memory Usage Report</Name>
        <Description></Description>
        <YAxisDescription>Memory used for {}</YAxisDescription>
        <YAxisFormat>N0</YAxisFormat>
      </Report>"""

template3 = """
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="RAM" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="CCMRAM" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="ROM" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="SRAM" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="SDRAM" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="FLASH" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="UNKNOWN" Message="" />
      <ResultLines Entity="{}" Count="{}" MeasureTypeKey="TOTAL" Message="" />
    </ReportResults>
</BuildResults>
"""

fileName = '{}/mem_usage.xml'.format(xmlLocation)
print ("writing file: ", fileName)
f = open(fileName, 'w')
print(template1.format(buildNumberKey), file=f)
print(template2.format(vsProject), file=f)
print(template3.format(vsProject, Ram, vsProject, Ccmram, vsProject, Rom, vsProject, Sram, vsProject, Sdram, vsProject, Flash, vsProject, Unknown, vsProject, Sum), file=f)

f.close()

