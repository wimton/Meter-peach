References to these folders can be found here (to be considered when updating, moving, removing, etc.):
* Python Library:
 - DEV\Environment\MSBuild\Nova.Common.Executable.props
 - DEV\Environment\MSBuild\Nova.Common.Properties.props (2x)
 - DEV\Environment\setenv.bat (2x)
 - DEV\Source\Shared\Components\NOS_OperatingSystem_Nsim\dependencies.props
 - In Visual Studio: Tools -> Options -> Python Tools -> Environment Options (+ according entries in how_to_get_started.docx)
* PyAsn1, PyAsn1-modules, PySerial, Selenium & xlrd:
 - DEV\Environment\MSBuild\Nova.Common.Properties.props
 - DEV\Environment\setenv.bat
 - DEV\Source\Local\Executables\_PyTestCaseDebug\_PyTestCaseDebug.pyproj
 
The Python installer will put pythonxx.dll (xx being the version numbers, e.g. python34.dll) into a system folder (system32 or SysWOW64 or even something else).
Move it to the new Python version's root folder.