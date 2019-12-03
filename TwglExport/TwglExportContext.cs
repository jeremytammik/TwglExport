#if COMPILE_EXPORTER
#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.Utility;
#endregion // Namespaces

namespace TwglExport
{
  public class TwglExportContext : IExportContext
  {
    /// <summary>
    /// Document being rendered.
    /// </summary>
    Document _doc;

    /// <summary>
    /// Center point of scene.
    /// </summary>
    XYZ _pmid;

    /// <summary>
    /// Transformation stack for 
    /// linked files and instances.
    /// </summary>
    /// 
    Stack<Transform> _transformationStack;

    /// <summary>
    /// The tree arrays passed over to NodeWegGL.
    /// </summary>
    List<int> _faceIndices;
    List<int> _faceVertices;
    List<double> _faceNormals;

    public List<int> FaceIndices { get { return _faceIndices; } }
    public List<int> FaceVertices { get { return _faceVertices; } }
    public List<double> FaceNormals { get { return _faceNormals; } }

    Transform CurrentTransform
    {
      get
      {
        return _transformationStack.Peek();
      }
    }

    public TwglExportContext( 
      Document document,
      XYZ pmid )
    {
      _doc = document;
      _pmid = pmid;
    }

    public bool Start()
    {
      _transformationStack = new Stack<Transform>();
      _transformationStack.Push( Transform.Identity );

      _faceIndices = new List<int>();
      _faceVertices = new List<int>();
      _faceNormals = new List<double>();

      return true;
    }

    public void Finish()
    {
    }

    public void OnPolymesh( PolymeshTopology polymesh )
    {
      Debug.WriteLine( string.Format(
        "    OnPolymesh: {0} points, {1} facets, {2} normals {3}",
        polymesh.NumberOfPoints,
        polymesh.NumberOfFacets,
        polymesh.NumberOfNormals,
        polymesh.DistributionOfNormals ) );

      IList<XYZ> pts = polymesh.GetPoints();
      IList<XYZ> normals = polymesh.GetNormals();

      Transform t = CurrentTransform;

      int nVertices = pts.Count;

      List<int> vertexCoordsMm = new List<int>(
        3 * nVertices );

      foreach( XYZ p in pts )
      {
        // Translate the entire element geometry
        // to the bounding box midpoint and scale 
        // to metric millimetres.

        XYZ q = t.OfPoint( p ) - _pmid;

        vertexCoordsMm.Add( Util.FootToMm( p.X ) );
        vertexCoordsMm.Add( Util.FootToMm( p.Y ) );
        vertexCoordsMm.Add( Util.FootToMm( p.Z ) );
      }

      int i = 0;
      int[] triangleIndices = new int[3];
      XYZ[] triangleCorners = new XYZ[3];

      foreach( PolymeshFacet facet
        in polymesh.GetFacets() )
      {
        Debug.WriteLine( string.Format(
          "      {0}: {1} {2} {3}", i++,
          facet.V1, facet.V2, facet.V3 ) );

        IList<int> vertices = facet.GetVertices();

        for( int j = 0; j < 3; ++j )
        {
          int k = vertices[j];
          triangleIndices[j] = k;
          triangleCorners[j] = pts[k];
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
          int nFaceVertices = _faceVertices.Count;

          Debug.Assert( nFaceVertices.Equals( _faceNormals.Count ),
            "expected equal number of face vertex and normal coordinates" );

          _faceIndices.Add( nFaceVertices / 3 );

          int i3 = triangleIndices[j] * 3;

          // Rotate the X, Y and Z directions, 
          // since the Z direction points upward 
          // in Revit as opposed to sideways or
          // outwards or forwards in WebGL.

          _faceVertices.Add( vertexCoordsMm[i3 + 1] );
          _faceVertices.Add( vertexCoordsMm[i3 + 2] );
          _faceVertices.Add( vertexCoordsMm[i3] );

          if( CmdElemGeom.RetainCurvedSurfaceFacets )
          {
            _faceNormals.Add( triangleNormal.Y );
            _faceNormals.Add( triangleNormal.Z );
            _faceNormals.Add( triangleNormal.X );
          }
          else
          {
            XYZ normal = normals[triangleIndices[j]];
            _faceNormals.Add( normal.Y );
            _faceNormals.Add( normal.Z );
            _faceNormals.Add( normal.X );
          }
        }
      }
    }

    public void OnMaterial( MaterialNode node )
    {
      Debug.WriteLine( "     --> On Material: "
        + node.MaterialId + ": " + node.NodeName );

      // OnMaterial method can be invoked for every 
      // single out-coming mesh even when the material 
      // has not actually changed. Thus it is usually
      // beneficial to store the current material and 
      // only get its attributes when the material 
      // actually changes.

      ElementId id = node.MaterialId;

      if( ElementId.InvalidElementId != id )
      {
        Element m = _doc.GetElement( node.MaterialId );
        //SetCurrentMaterial( m.UniqueId );
      }
    }

    /// <summary>
    /// This method is invoked many times during the 
    /// export process. Return false to continue.
    /// </summary>
    public bool IsCanceled()
    {
      return false;
    }

    public void OnDaylightPortal( DaylightPortalNode node )
    {
      Debug.WriteLine( "OnDaylightPortal: " + node.NodeName );
      Asset asset = node.GetAsset();
      Debug.WriteLine( "OnDaylightPortal: Asset:"
        + ( ( asset != null ) ? asset.Name : "Null" ) );
    }

    public void OnRPC( RPCNode node )
    {
      Debug.WriteLine( "OnRPC: " + node.NodeName );
      Asset asset = node.GetAsset();
      Debug.WriteLine( "OnRPC: Asset:"
        + ( ( asset != null ) ? asset.Name : "Null" ) );
    }

    public RenderNodeAction OnViewBegin( ViewNode node )
    {
      Debug.WriteLine( "OnViewBegin: "
        + node.NodeName + "(" + node.ViewId.IntegerValue
        + "): LOD: " + node.LevelOfDetail );

      return RenderNodeAction.Proceed;
    }

    public void OnViewEnd( ElementId elementId )
    {
      Debug.WriteLine( "OnViewEnd: Id: " 
        + elementId.IntegerValue );

      // Note: This method is invoked even 
      // for a view that was skipped.
    }

    public RenderNodeAction OnElementBegin(
      ElementId elementId )
    {
      Element e = _doc.GetElement( elementId );
      string uid = e.UniqueId;

      Debug.WriteLine( string.Format(
        "OnElementBegin: id {0} category {1} name {2}",
        elementId.IntegerValue, e.Category.Name, e.Name ) );

      //if( null == e.Category )
      //{
      //  Debug.WriteLine( "\r\n*** Non-category element!\r\n" );
      //  return RenderNodeAction.Skip;
      //}

      return RenderNodeAction.Proceed;
    }

    public void OnElementEnd(
      ElementId id )
    {
      // Note: this method is invoked even for 
      // elements that were skipped.

      Element e = _doc.GetElement( id );
      string uid = e.UniqueId;

      Debug.WriteLine( string.Format(
        "OnElementEnd: id {0} category {1} name {2}",
        id.IntegerValue, e.Category.Name, e.Name ) );
    }

    public RenderNodeAction OnFaceBegin( 
      FaceNode node )
    {
      // This method is invoked only if the 
      // custom exporter was set to include faces.

      Debug.Assert( false, 
        "we set exporter.IncludeFaces false" );

      Debug.WriteLine( "  OnFaceBegin: " 
        + node.NodeName );

      return RenderNodeAction.Proceed;
    }

    public void OnFaceEnd( FaceNode node )
    {
      // This method is invoked only if the 
      // custom exporter was set to include faces.

      Debug.Assert( false, 
        "we set exporter.IncludeFaces false" );

      Debug.WriteLine( "  OnFaceEnd: " 
        + node.NodeName );

      // Note: This method is invoked even 
      // for faces that were skipped.
    }

    public RenderNodeAction OnInstanceBegin( 
      InstanceNode node )
    {
      Debug.WriteLine( "  OnInstanceBegin: " 
        + node.NodeName + " symbol: " 
        + node.GetSymbolId().IntegerValue );

      // This method marks the start of 
      // processing a family instance.

      _transformationStack.Push( 
        CurrentTransform.Multiply( 
          node.GetTransform() ) );

      // We can either skip this instance 
      // or proceed with rendering it.

      return RenderNodeAction.Proceed;
    }

    public void OnInstanceEnd( InstanceNode node )
    {
      Debug.WriteLine( "  OnInstanceEnd: " 
        + node.NodeName );

      // Note: This method is invoked even 
      // for instances that were skipped.

      _transformationStack.Pop();
    }

    public RenderNodeAction OnLinkBegin( 
      LinkNode node )
    {
      Debug.WriteLine( "  OnLinkBegin: " 
        + node.NodeName + " Document: " 
        + node.GetDocument().Title + ": Id: " 
        + node.GetSymbolId().IntegerValue );

      _transformationStack.Push( 
        CurrentTransform.Multiply( 
          node.GetTransform() ) );

      return RenderNodeAction.Proceed;
    }

    public void OnLinkEnd( LinkNode node )
    {
      Debug.WriteLine( "  OnLinkEnd: " 
        + node.NodeName );

      // Note: This method is invoked even 
      // for instances that were skipped.

      _transformationStack.Pop();
    }

    public void OnLight( LightNode node )
    {
      Debug.WriteLine( "OnLight: " + node.NodeName );
      Asset asset = node.GetAsset();
      Debug.WriteLine( "OnLight: Asset:" 
        + ( ( asset != null ) ? asset.Name : "Null" ) );
    }
  }
}
#endif // COMPILE_EXPORTER