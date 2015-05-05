using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle( "TwglExport" )]
[assembly: AssemblyDescription( "Revit add-in to export 3D element geometry to Node.js WebGL viewer" )]
[assembly: AssemblyConfiguration( "" )]
[assembly: AssemblyCompany( "Autodesk Inc." )]
[assembly: AssemblyProduct( "TwglExport" )]
[assembly: AssemblyCopyright( "Copyright 2015 © Jeremy Tammik Autodesk Inc." )]
[assembly: AssemblyTrademark( "" )]
[assembly: AssemblyCulture( "" )]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible( false )]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid( "321044f7-b0b2-4b1c-af18-e71a19252be0" )]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
//
// 2015-04-13 2015.0.0.0 initial implementation
// 2015-04-14 2015.0.0.1 added RetainCurvedSurfaceFacets and implemented face smoothing 
// 2015-04-27 2015.0.0.2 started putting together JSON string to directly launch the viewer
// 2015-04-28 2015.0.0.2 invoke node.js WebGL viewer web server directly via HTTP POST request
// 2015-04-29 2015.0.0.3 invoke remote node.js WebGL viewer web server directly via HTTP POST request
// 2015-05-04 2015.0.0.4 implemented a skeleton custom exporter context
// 2015-05-04 2015.0.0.5 implemented new command to drive custom exporter
// 2015-05-05 2015.0.0.6 implemented and tested custom exporter, HTML is correctly generated, but HTML viewing of the final result is failing due to COR policy
//
[assembly: AssemblyVersion( "2015.0.0.6" )]
[assembly: AssemblyFileVersion( "2015.0.0.6" )]
