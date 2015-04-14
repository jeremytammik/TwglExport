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
#endregion

namespace TwglExport
{
  [Transaction( TransactionMode.ReadOnly )]
  public class Command : IExternalCommand
  {
    const double _mm_per_inch = 25.4;
    const double _inch_per_foot = 12;
    const double _foot_to_mm = _inch_per_foot * _mm_per_inch;

    static int FootToMm( double a )
    {
      double one_half = a > 0 ? 0.5 : -0.5;
      return (int) (a * _foot_to_mm + one_half);
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
            // we cannot precalculate normals.
            //List<double> normals = new List<double>( 3 * nVertices );

            foreach( XYZ v in vertices )
            {
              // Translate to bounding box midpoint.

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
                triangleIndices[j] = (int) triangle.get_Index( j );
              }

              XYZ p = vertices[triangleIndices[0]];
              XYZ q = vertices[triangleIndices[1]];
              XYZ r = vertices[triangleIndices[2]];

              XYZ normal = ( q - p ).CrossProduct( r - p ).Normalize();

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
                faceNormals.Add( normal.Y );
                faceNormals.Add( normal.Z );
                faceNormals.Add( normal.X );
              }
            }
          }

          // Scale and translate the vertices to a 
          // [-1,1] cube centered around the origin.

          double scale = 2.0 / FootToMm( vsize.GetLength() );

          Debug.Print( "position: [{0}],", 
            string.Join( ", ", 
              faceVertices.ConvertAll<string>( 
                i => ( i * scale ).ToString( "0.##" ) ) ) );

          Debug.Print( "normal: [{0}],", 
            string.Join( ", ", 
              faceNormals.ConvertAll<string>( 
                f => f.ToString( "0.##" ) ) ) );

          Debug.Print( "indices: [{0}],", 
            string.Join( ", ", 
              faceIndices.ConvertAll<string>( 
                i => i.ToString() ) ) );
        }
      }
      return Result.Succeeded;
    }
  }
}
