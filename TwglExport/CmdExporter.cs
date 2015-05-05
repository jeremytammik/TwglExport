#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace TwglExport
{
  [Transaction( TransactionMode.ReadOnly )]
  public class CmdExporter : IExternalCommand
  {
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

      using( TransactionGroup tg 
        = new TransactionGroup( doc ) )
      {
        // Create 3D view

        ViewFamilyType viewType
          = new FilteredElementCollector( doc )
            .OfClass( typeof( ViewFamilyType ) )
            .OfType<ViewFamilyType>()
            .FirstOrDefault( x => x.ViewFamily 
              == ViewFamily.ThreeDimensional );

        View3D view;

        using( Transaction t = new Transaction( doc ) )
        {
          t.Start( "Create 3D View" );

          view = View3D.CreateIsometric(
            doc, viewType.Id );

          t.Commit();

          view.IsolateElementTemporary( e.Id );

          t.Commit();

          TwglExportContext context 
            = new TwglExportContext( doc, pmid );

          CustomExporter exporter 
            = new CustomExporter( doc, context );

          // Note: Excluding faces just suppresses the 
          // OnFaceBegin calls, not the actual processing 
          // of face tessellation. Meshes of the faces 
          // will still be received by the context.

          exporter.IncludeFaces = false;

          exporter.ShouldStopOnError = false;

          exporter.Export( view );

          // Scale the vertices to a [-1,1] cube 
          // centered around the origin. Translation
          // to the origin was already performed above.

          double scale = 2.0 / Util.FootToMm( 
            Util.MaxCoord( vsize ) );

          string json_geometry_data
            = CmdElemGeom.GetJsonGeometryData( scale, 
              context.FaceIndices, context.FaceVertices, 
              context.FaceNormals );

          CmdElemGeom.DisplayWgl( json_geometry_data );

        }
        // Roll back entire operation.

        //tg.Commit();
      }
      return Result.Succeeded;
    }
  }
}
