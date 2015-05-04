# TwglExport

A tiny Revit add-in to export element geometry to
[WebGL](https://www.khronos.org/webgl), e.g., the
[NodeWebGL](https://github.com/jeremytammik/NodeWebGL) minimal Node.js WebGL viewer app.

Tiny twice over, at both ends: a small and simple Revit add-in making use of a Node.js WebGL server implemented using the Tiny WebGL library
[TWGL](http://twgljs.org).

For a detailed description, please refer to
[The Building Coder](http://thebuildingcoder.typepad.com) and
[The 3D Web Coder](http://the3dwebcoder.typepad.com).


## Todo

- Use a
[custom exporter](http://thebuildingcoder.typepad.com/blog/about-the-author.html#5.1)
instead of traversing the element geometry.

- Implement support to directly drive the [NodeWebGL](https://github.com/jeremytammik/NodeWebGL) REST API -- 
Initial solution completed with [release 2015.0.0.3](https://github.com/jeremytammik/TwglExport/releases/tag/2015.0.0.3).

## Author

Jeremy Tammik, [The Building Coder](http://thebuildingcoder.typepad.com) and
[The 3D Web Coder](http://the3dwebcoder.typepad.com), Autodesk Inc.


## License

This sample is licensed under the terms of the [MIT License](http://www.apache.org/licenses/LICENSE-2.0).
Please see the [LICENSE](LICENSE) file for full details.