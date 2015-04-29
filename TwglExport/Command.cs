#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Net;
using System.IO;
using System.Text;
#endregion

namespace TwglExport
{
  [Transaction( TransactionMode.ReadOnly )]
  public class Command : IExternalCommand
  {
    /// <summary>
    /// Toggle between a local server and
    /// a remote Heroku-hosted one.
    /// </summary>
    static public bool UseLocalServer = false;

    /// <summary>
    /// If true, individual curved surface facets are
    /// retained, otherwise (default) smoothing is 
    /// applied.
    /// </summary>
    static public bool RetainCurvedSurfaceFacets = false;

    // Unit conversion factors.

    const double _mm_per_inch = 25.4;
    const double _inch_per_foot = 12;
    const double _foot_to_mm = _inch_per_foot * _mm_per_inch;

    /// <summary>
    /// Convert the given value from 
    /// imperial feet to metric millimetres.
    /// </summary>
    static int FootToMm( double a )
    {
      double one_half = a > 0 ? 0.5 : -0.5;
      return (int) ( a * _foot_to_mm + one_half );
    }

    /// <summary>
    /// Return the maximum absolute coordinate 
    /// of the given point or vector.
    /// </summary>
    static double MaxCoord( XYZ a )
    {
      double d = Math.Abs( a.X );
      d = Math.Max( d, Math.Abs( a.Y ) );
      d = Math.Max( d, Math.Abs( a.Z ) );
      return d;
    }

    /// <summary>
    /// Invoke the node.js WebGL viewer web server.
    /// Use a local or global base URL and an HTTP POST
    /// request passing the 3D geometry data as body.
    /// </summary>
    void DisplayWgl( string json_geometry_data )
    {
      string base_url = UseLocalServer
        ? "http://127.0.0.1:5000"
        : "https://nameless-harbor-7576.herokuapp.com";

      string api_route = "api/v2";

      string uri = base_url + "/" + api_route;

      HttpWebRequest req = WebRequest.Create( uri ) as HttpWebRequest;

      req.KeepAlive = false;
      req.Method = WebRequestMethods.Http.Post;

      // Turn our request string into a byte stream.

      byte[] postBytes = Encoding.UTF8.GetBytes( json_geometry_data );

      req.ContentLength = postBytes.Length;

      // Specify content type.

      req.ContentType = "application/json; charset=UTF-8"; // or just "text/json"?
      req.Accept = "application/json";
      req.ContentLength = postBytes.Length;

      Stream requestStream = req.GetRequestStream();
      requestStream.Write( postBytes, 0, postBytes.Length );
      requestStream.Close();

      HttpWebResponse res = req.GetResponse() as HttpWebResponse;

      string result;

      using( StreamReader reader = new StreamReader(
        res.GetResponseStream() ) )
      {
        result = reader.ReadToEnd();
      }

      string filename = Path.GetTempFileName();
      filename = Path.ChangeExtension( filename, "html" );

      using( StreamWriter writer = File.CreateText( filename ) )
      {
        writer.Write( result );
        writer.Close();
      }

      System.Diagnostics.Process.Start( filename );
    }

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;
      Selection sel = uidoc.Selection;
      ICollection<ElementId> ids = sel.GetElementIds();

      if( 1 != ids.Count )
      {
        message = "Please select an element to export to TWGL.";
        return Result.Failed;
      }

      Element e = null;

      foreach( ElementId id in ids )
      {
        e = doc.GetElement( id );
      }

      // Determine bounding box in order to translate
      // all coordinates to bounding box midpoint.

      BoundingBoxXYZ bb = e.get_BoundingBox( null );
      XYZ pmin = bb.Min;
      XYZ pmax = bb.Max;
      XYZ vsize = pmax - pmin;
      XYZ pmid = pmin + 0.5 * vsize;

      Options opt = new Options();
      GeometryElement geo = e.get_Geometry( opt );

      List<int> faceIndices = new List<int>();
      List<int> faceVertices = new List<int>();
      List<double> faceNormals = new List<double>();
      int[] triangleIndices = new int[3];
      XYZ[] triangleCorners = new XYZ[3];

      foreach( GeometryObject obj in geo )
      {
        Solid solid = obj as Solid;

        if( solid != null && 0 < solid.Faces.Size )
        {
          faceIndices.Clear();
          faceVertices.Clear();
          faceNormals.Clear();

          foreach( Face face in solid.Faces )
          {
            Mesh mesh = face.Triangulate();

            int nTriangles = mesh.NumTriangles;

            IList<XYZ> vertices = mesh.Vertices;

            int nVertices = vertices.Count;

            List<int> vertexCoordsMm = new List<int>( 3 * nVertices );

            // A vertex may be reused several times with 
            // different normals for different faces, so 
            // we cannot precalculate normals per vertex.
            //List<double> normals = new List<double>( 3 * nVertices );

            foreach( XYZ v in vertices )
            {
              // Translate the entire element geometry
              // to the bounding box midpoint and scale 
              // to metric millimetres.

              XYZ p = v - pmid;

              vertexCoordsMm.Add( FootToMm( p.X ) );
              vertexCoordsMm.Add( FootToMm( p.Y ) );
              vertexCoordsMm.Add( FootToMm( p.Z ) );
            }

            for( int i = 0; i < nTriangles; ++i )
            {
              MeshTriangle triangle = mesh.get_Triangle( i );

              for( int j = 0; j < 3; ++j )
              {
                int k = (int) triangle.get_Index( j );
                triangleIndices[j] = k;
                triangleCorners[j] = vertices[k];
              }

              // Calculate constant triangle facet normal.

              XYZ v = triangleCorners[1]
                - triangleCorners[0];
              XYZ w = triangleCorners[2]
                - triangleCorners[0];
              XYZ triangleNormal = v
                .CrossProduct( w )
                .Normalize();

              for( int j = 0; j < 3; ++j )
              {
                int nFaceVertices = faceVertices.Count;

                Debug.Assert( nFaceVertices.Equals( faceNormals.Count ),
                  "expected equal number of face vertex and normal coordinates" );

                faceIndices.Add( nFaceVertices / 3 );

                int i3 = triangleIndices[j] * 3;

                // Rotate the X, Y and Z directions, 
                // since the Z direction points upward 
                // in Revit as opposed to sideways or
                // outwards or forwards in WebGL.

                faceVertices.Add( vertexCoordsMm[i3 + 1] );
                faceVertices.Add( vertexCoordsMm[i3 + 2] );
                faceVertices.Add( vertexCoordsMm[i3] );

                if( RetainCurvedSurfaceFacets )
                {
                  faceNormals.Add( triangleNormal.Y );
                  faceNormals.Add( triangleNormal.Z );
                  faceNormals.Add( triangleNormal.X );
                }
                else
                {
                  UV uv = face.Project(
                    triangleCorners[j] ).UVPoint;

                  XYZ normal = face.ComputeNormal( uv );

                  faceNormals.Add( normal.Y );
                  faceNormals.Add( normal.Z );
                  faceNormals.Add( normal.X );
                }
              }
            }
          }

          // Scale the vertices to a [-1,1] cube 
          // centered around the origin. Translation
          // to the origin was already performed above.

          double scale = 2.0 / FootToMm( MaxCoord( vsize ) );

          string sposition = string.Join( ", ",
            faceVertices.ConvertAll<string>(
              i => ( i * scale ).ToString( "0.##" ) ) );

          string snormal = string.Join( ", ",
            faceNormals.ConvertAll<string>(
              f => f.ToString( "0.##" ) ) );

          string sindices = string.Join( ", ",
            faceIndices.ConvertAll<string>(
              i => i.ToString() ) );

          Debug.Print( "position: [{0}],", sposition );
          Debug.Print( "normal: [{0}],", snormal );
          Debug.Print( "indices: [{0}],", sindices );

          //string json_geometry_data = string.Format(
          //  "{ \"position\": [{0}],\n\"normal\": [{1}], \"indices\": [{2}] }",
          //  sposition, snormal, sindices );

          string json_geometry_data =
            "{ \"position\": [" + sposition
            + "],\n\"normal\": [" + snormal
            + "],\n\"indices\": [" + sindices
            + "] }";

          Debug.Print( "json: " + json_geometry_data );

          DisplayWgl( json_geometry_data );
        }
      }
      return Result.Succeeded;
    }
  }
}
