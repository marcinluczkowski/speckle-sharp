﻿using Autodesk.DesignScript.Geometry;
using Objects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Speckle.Core.Models;
using System.Runtime.CompilerServices;
using DS = Autodesk.DesignScript.Geometry;
using Objects.Primitive;
using Point = Objects.Geometry.Point;
using Vector = Objects.Geometry.Vector;
using Line = Objects.Geometry.Line;
using Plane = Objects.Geometry.Plane;
using Circle = Objects.Geometry.Circle;
using Arc = Objects.Geometry.Arc;
using Ellipse = Objects.Geometry.Ellipse;
using Curve = Objects.Geometry.Curve;
using Mesh = Objects.Geometry.Mesh;
using Objects;
using Surface = Objects.Geometry.Surface;
using Speckle.Core.Kits;

namespace Objects.Converter.Dynamo
{
  //Original author 
  //     Name: Alvaro Ortega Pickmans 
  //     Github: alvpickmans
  //Code form: https://github.com/speckleworks/SpeckleCoreGeometry/blob/master/SpeckleCoreGeometryDynamo/Conversions.cs
  public partial class ConverterDynamo
  {
    #region Points


    /// <summary>
    /// DS Point to SpecklePoint
    /// </summary>
    /// <param name="pt"></param>
    /// <returns></returns>
    public Point PointToSpeckle(DS.Point pt)
    {

      var point = new Point(pt.X, pt.Y, pt.Z, ModelUnits);
      CopyProperties(point, pt);
      return point;
    }

    /// <summary>
    /// Speckle Point to DS Point
    /// </summary>
    /// <param name="pt"></param>
    /// <returns></returns>
    /// 
    public DS.Point PointToNative(Point pt)
    {
      var point = DS.Point.ByCoordinates(
        ScaleToNative(pt.value[0], pt.units), ScaleToNative(pt.value[1], pt.units), ScaleToNative(pt.value[2], pt.units));

      return point.SetDynamoProperties<DS.Point>(GetDynamicMembersFromBase(pt));
    }


    /// <summary>
    /// Array of point coordinates to array of DS Points
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public DS.Point[] ArrayToPointList(IEnumerable<double> arr, string units)
    {
      if (arr.Count() % 3 != 0) throw new Exception("Array malformed: length%3 != 0.");

      DS.Point[] points = new DS.Point[arr.Count() / 3];
      var asArray = arr.ToArray();
      for (int i = 2, k = 0; i < arr.Count(); i += 3)
        points[k++] = DS.Point.ByCoordinates(ScaleToNative(asArray[i - 2], units), ScaleToNative(asArray[i - 1], units), ScaleToNative(asArray[i], units));

      return points;
    }

    /// <summary>
    /// Array of DS Points to array of point coordinates
    /// </summary>
    /// <param name="points"></param>
    /// <returns></returns>
    public double[] PointListToFlatArray(IEnumerable<DS.Point> points)
    {
      return points.SelectMany(pt => PointToArray(pt)).ToArray();
    }

    public double[] PointToArray(DS.Point pt)
    {
      return new double[] { pt.X, pt.Y, pt.Z };
    }

    #endregion

    #region Vectors

    /// <summary>
    /// DS Vector to Vector
    /// </summary>
    /// <param name="vc"></param>
    /// <returns></returns>
    public Vector VectorToSpeckle(DS.Vector vc)
    {
      return new Vector(vc.X, vc.Y, vc.Z, ModelUnits);
    }

    /// <summary>
    /// Vector to DS Vector
    /// </summary>
    /// <param name="vc"></param>
    /// <returns></returns>
    public DS.Vector VectorToNative(Vector vc)
    {
      return DS.Vector.ByCoordinates(
        ScaleToNative(vc.value[0], vc.units),
        ScaleToNative(vc.value[1], vc.units),
        ScaleToNative(vc.value[2], vc.units));
    }

    /// <summary>
    /// DS Vector to array of coordinates
    /// </summary>
    /// <param name="vc"></param>
    /// <returns></returns>
    //public double[] VectorToArray(DS.Vector vc)
    //{
    //  return new double[] { vc.X, vc.Y, vc.Z };
    //}

    /// <summary>
    /// Array of coordinates to DS Vector
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    //public DS.Vector VectorToVector(double[] arr)
    //{
    //  return DS.Vector.ByCoordinates(arr[0], arr[1], arr[2]);
    //}

    #endregion

    #region Planes

    /// <summary>
    /// DS Plane to Plane
    /// </summary>
    /// <param name="plane"></param>
    /// <returns></returns>
    public Plane PlaneToSpeckle(DS.Plane plane)
    {
      var p = new Plane(
        PointToSpeckle(plane.Origin),
        VectorToSpeckle(plane.Normal),
        VectorToSpeckle(plane.XAxis),
        VectorToSpeckle(plane.YAxis),
        ModelUnits);
      CopyProperties(p, plane);
      return p;
    }

    /// <summary>
    /// Plane to DS Plane
    /// </summary>
    /// <param name="plane"></param>
    /// <returns></returns>
    public DS.Plane PlaneToNative(Plane plane)
    {
      var pln = DS.Plane.ByOriginXAxisYAxis(
        PointToNative(plane.origin),
        VectorToNative(plane.xdir),
        VectorToNative(plane.ydir));

      return pln.SetDynamoProperties<DS.Plane>(GetDynamicMembersFromBase(plane));
    }

    #endregion

    #region Linear

    /// <summary>
    /// DS Line to SpeckleLine
    /// </summary>
    /// <param name="line"></param>
    /// <returns></returns>
    public Line LineToSpeckle(DS.Line line)
    {
      var l = new Line(
       PointListToFlatArray(new DS.Point[] { line.StartPoint, line.EndPoint }),
        ModelUnits);

      CopyProperties(l, line);
      l.length = line.Length;
      l.bbox = BoxToSpeckle(line.BoundingBox.ToCuboid());
      return l;
    }

    /// <summary>
    /// SpeckleLine to DS Line
    /// </summary>
    /// <param name="line"></param>
    /// <returns></returns>
    public DS.Line LineToNative(Line line)
    {
      var pts = ArrayToPointList(line.value, line.units);
      var ln = DS.Line.ByStartPointEndPoint(pts[0], pts[1]);

      pts.ForEach(pt => pt.Dispose());

      return ln.SetDynamoProperties<DS.Line>(GetDynamicMembersFromBase(line));
    }

    /// <summary>
    /// DS Polygon to closed SpecklePolyline
    /// </summary>
    /// <param name="polygon"></param>
    /// <returns></returns>
    public Polyline PolylineToSpeckle(DS.Polygon polygon)
    {
      var poly = new Polyline(PointListToFlatArray(polygon.Points), ModelUnits)
      {
        closed = true,
      };
      CopyProperties(poly, polygon);
      poly.length = polygon.Length;
      poly.bbox = BoxToSpeckle(polygon.BoundingBox.ToCuboid());
      return poly;
    }


    /// <summary>
    /// DS Rectangle to SpecklePolyline
    /// </summary>
    /// <param name="rect"></param>
    /// <returns></returns>
    public Polyline PolylineToSpeckle(DS.Rectangle rectangle)
    {
      var rect = PolylineToSpeckle(rectangle as DS.Polygon);

      CopyProperties(rect, rectangle);
      return rect;
    }

    /// <summary>
    /// SpecklePolyline to DS Rectangle if closed , four points and sides parallel; 
    /// DS Polygon if closed or DS Polycurve otherwise
    /// </summary>
    /// <param name="polyline"></param>
    /// <returns></returns>
    public DS.Curve PolylineToNative(Polyline polyline)
    {
      var points = ArrayToPointList(polyline.value, polyline.units);
      if (polyline.closed)
        return DS.PolyCurve.ByPoints(points).CloseWithLine()
          .SetDynamoProperties<DS.PolyCurve>(GetDynamicMembersFromBase(polyline));

      return PolyCurve.ByPoints(points).SetDynamoProperties<PolyCurve>(GetDynamicMembersFromBase(polyline));
    }

    #endregion

    #region Curves

    /// <summary>
    /// DS Circle to SpeckleCircle.
    /// </summary>
    /// <param name="circ"></param>
    /// <returns></returns>
    public Circle CircleToSpeckle(DS.Circle circ)
    {
      using (DS.Vector xAxis = DS.Vector.ByTwoPoints(circ.CenterPoint, circ.StartPoint))
      using (DS.Plane plane = DS.Plane.ByOriginNormalXAxis(circ.CenterPoint, circ.Normal, xAxis))
      {
        var myCircle = new Circle(PlaneToSpeckle(plane), circ.Radius, ModelUnits);
        CopyProperties(myCircle, circ);
        myCircle.length = circ.Length;
        myCircle.bbox = BoxToSpeckle(circ.BoundingBox.ToCuboid());
        return myCircle;
      }
    }

    /// <summary>
    /// SpeckleCircle to DS Circle. Rotating the circle is due to a bug in ProtoGeometry
    /// that will be solved on Dynamo 2.1.
    /// </summary>
    /// <param name="circ"></param>
    /// <returns></returns>
    public DS.Circle CircleToNative(Circle circ)
    {
      using (DS.Plane basePlane = PlaneToNative(circ.plane))
      using (DS.Circle preCircle = DS.Circle.ByPlaneRadius(basePlane, ScaleToNative(circ.radius.Value, circ.units)))
      using (DS.Vector preXvector = DS.Vector.ByTwoPoints(preCircle.CenterPoint, preCircle.StartPoint))
      {
        double angle = preXvector.AngleAboutAxis(basePlane.XAxis, basePlane.Normal);
        var circle = (DS.Circle)preCircle.Rotate(basePlane, angle);

        return circle.SetDynamoProperties<DS.Circle>(GetDynamicMembersFromBase(circ));
      }
    }

    /// <summary>
    /// DS Arc to SpeckleArc
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public Arc ArcToSpeckle(DS.Arc a)
    {
      using (DS.Vector xAxis = DS.Vector.ByTwoPoints(a.CenterPoint, a.StartPoint))
      using (DS.Plane basePlane = DS.Plane.ByOriginNormalXAxis(a.CenterPoint, a.Normal, xAxis))
      {
        var arc = new Arc(
          PlaneToSpeckle(basePlane),
          a.Radius,
          0, // This becomes 0 as arcs are interpreted to start from the plane's X axis.
          a.SweepAngle.ToRadians(),
          a.SweepAngle.ToRadians(),
          ModelUnits
        );

        CopyProperties(arc, a);
        arc.length = a.Length;
        arc.bbox = BoxToSpeckle(a.BoundingBox.ToCuboid());
        return arc;
      }
    }

    /// <summary>
    /// SpeckleArc to DS Arc
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public DS.Arc ArcToNative(Arc a)
    {
      using (DS.Plane basePlane = PlaneToNative(a.plane))
      using (DS.Point startPoint = (DS.Point)basePlane.Origin.Translate(basePlane.XAxis, ScaleToNative(a.radius.Value, a.units)))
      {
        var arc = DS.Arc.ByCenterPointStartPointSweepAngle(
          basePlane.Origin,
          startPoint,
          a.angleRadians.Value.ToDegrees(),
          basePlane.Normal
        );
        return arc.SetDynamoProperties<DS.Arc>(GetDynamicMembersFromBase(a));
      }
    }

    /// <summary>
    /// DS Ellipse to SpeckleEllipse
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public Ellipse EllipseToSpeckle(DS.Ellipse e)
    {
      using (DS.Plane basePlane = DS.Plane.ByOriginNormalXAxis(e.CenterPoint, e.Normal, e.MajorAxis))
      {
        var ellipse = new Ellipse(
          PlaneToSpeckle(basePlane),
          e.MajorAxis.Length,
          e.MinorAxis.Length,
          new Interval(e.StartParameter(), e.EndParameter()),
          null,
          ModelUnits);

        CopyProperties(ellipse, e);

        ellipse.length = e.Length;
        ellipse.bbox = BoxToSpeckle(e.BoundingBox.ToCuboid());
        
        return ellipse;
      }
    }

    /// <summary>
    /// SpeckleEllipse to DS Ellipse
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public DS.Curve EllipseToNative(Ellipse e)
    {
      if (e.trimDomain != null)
      {
        // Curve is an ellipse arc
        var ellipseArc = DS.EllipseArc.ByPlaneRadiiAngles(
          PlaneToNative(e.plane),
          ScaleToNative(e.firstRadius.Value, e.units),
          ScaleToNative(e.secondRadius.Value, e.units),
          e.trimDomain.start.Value * 360 / (2 * Math.PI),
          (double)(e.trimDomain.end - e.trimDomain.start) * 360 / (2 * Math.PI));
        return ellipseArc;
      }
      else
      {
        // Curve is an ellipse
        var ellipse = DS.Ellipse.ByPlaneRadii(
            PlaneToNative(e.plane),
            ScaleToNative(e.firstRadius.Value, e.units),
            ScaleToNative(e.secondRadius.Value, e.units)
        );
        ellipse.SetDynamoProperties<DS.Ellipse>(GetDynamicMembersFromBase(e));
        return ellipse;
      }
    }

    /// <summary>
    /// DS EllipseArc to Speckle Ellipse
    /// </summary>
    /// <param name="arc"></param>
    /// <returns></returns>
    public Ellipse EllipseToSpeckle(EllipseArc arc)
    {
      var ellipArc = new Ellipse(
        PlaneToSpeckle(arc.Plane),
        arc.MajorAxis.Length,
        arc.MinorAxis.Length,
        new Interval(0, 2 * Math.PI),
        new Interval(arc.StartAngle, arc.StartAngle + arc.SweepAngle),
        ModelUnits);

      CopyProperties(ellipArc, arc);

      ellipArc.length = arc.Length;
      ellipArc.bbox = BoxToSpeckle(arc.BoundingBox.ToCuboid());
      
      return ellipArc;
    }


    /// <summary>
    /// DS Polycurve to SpecklePolyline if all curves are linear
    /// SpecklePolycurve otherwise
    /// </summary>
    /// <param name="polycurve"></param>
    /// <returns name="speckleObject"></returns>
    public Base PolycurveToSpeckle(PolyCurve polycurve)
    {
      if (polycurve.IsPolyline())
      {
        var points = polycurve.Curves().SelectMany(c => PointToArray(c.StartPoint)).ToList();
        points.AddRange(PointToArray(polycurve.Curves().Last().EndPoint));
        var poly = new Polyline(points, ModelUnits);

        CopyProperties(poly, polycurve);
        poly.length = polycurve.Length;
        poly.bbox = BoxToSpeckle(polycurve.BoundingBox.ToCuboid());
        
        return poly;
      }
      else
      {
        Polycurve spkPolycurve = new Polycurve();
        CopyProperties(spkPolycurve, polycurve);

        spkPolycurve.segments = polycurve.Curves().Select(c => (ICurve)CurveToSpeckle(c)).ToList();
        
        spkPolycurve.length = polycurve.Length;
        spkPolycurve.bbox = BoxToSpeckle(polycurve.BoundingBox.ToCuboid());

        return spkPolycurve;
      }
    }

    public PolyCurve PolycurveToNative(Polycurve polycurve)
    {
      DS.Curve[] curves = new DS.Curve[polycurve.segments.Count];
      for (var i = 0; i < polycurve.segments.Count; i++)
      {
        switch (polycurve.segments[i])
        {
          case Line curve:
            curves[i] = LineToNative(curve);
            break;
          case Arc curve:
            curves[i] = ArcToNative(curve);
            break;
          case Circle curve:
            curves[i] = CircleToNative(curve);
            break;
          case Ellipse curve:
            curves[i] = EllipseToNative(curve);
            break;
          case Polycurve curve:
            curves[i] = PolycurveToNative(curve);
            break;
          case Polyline curve:
            curves[i] = PolylineToNative(curve);
            break;
          case Curve curve:
            curves[i] = CurveToNative(curve);
            break;
        }
      }

      PolyCurve polyCrv = null;
      if (curves.Any())
      {
        polyCrv = PolyCurve.ByJoinedCurves(curves);
        polyCrv = polyCrv.SetDynamoProperties<PolyCurve>(GetDynamicMembersFromBase(polycurve));
      }

      return polyCrv;
    }

    public Base CurveToSpeckle(DS.Curve curve)
    {
      Base speckleCurve;
      if (curve.IsLinear())
      {
        using (DS.Line line = curve.GetAsLine())
        {
          speckleCurve = LineToSpeckle(line);
        }
      }
      else if (curve.IsArc())
      {
        using (DS.Arc arc = curve.GetAsArc())
        {
          speckleCurve = ArcToSpeckle(arc);
        }
      }
      else if (curve.IsCircle())
      {
        using (DS.Circle circle = curve.GetAsCircle())
        {
          speckleCurve = CircleToSpeckle(circle);
        }
      }
      else if (curve.IsEllipse())
      {
        using (DS.Ellipse ellipse = curve.GetAsEllipse())
        {
          speckleCurve = EllipseToSpeckle(ellipse);
        }
      }
      else
      {
        speckleCurve = CurveToSpeckle(curve.ToNurbsCurve());
      }

      CopyProperties(speckleCurve, curve);
      return speckleCurve;
    }

    public NurbsCurve CurveToNative(Curve curve)
    {
      var points = ArrayToPointList(curve.points, curve.units);
      var dsKnots = curve.knots;
      dsKnots.Insert(0, dsKnots.First());
      dsKnots.Add(dsKnots.Last());

      NurbsCurve nurbsCurve = NurbsCurve.ByControlPointsWeightsKnots(
        points,
        curve.weights.ToArray(),
        dsKnots.ToArray(),
        curve.degree
      );

      return nurbsCurve.SetDynamoProperties<NurbsCurve>(GetDynamicMembersFromBase(curve));
    }

    public Base CurveToSpeckle(NurbsCurve curve)
    {
      Base speckleCurve;
      if (curve.IsLinear())
      {
        using (DS.Line line = curve.GetAsLine())
        {
          speckleCurve = LineToSpeckle(line);
        }
      }
      else if (curve.IsArc())
      {
        using (DS.Arc arc = curve.GetAsArc())
        {
          speckleCurve = ArcToSpeckle(arc);
        }
      }
      else if (curve.IsCircle())
      {
        using (DS.Circle circle = curve.GetAsCircle())
        {
          speckleCurve = CircleToSpeckle(circle);
        }
      }
      else if (curve.IsEllipse())
      {
        using (DS.Ellipse ellipse = curve.GetAsEllipse())
        {
          speckleCurve = EllipseToSpeckle(ellipse);
        }
      }
      else
      {
        // SpeckleCurve DisplayValue
        DS.Curve[] curves = curve.ApproximateWithArcAndLineSegments();
        List<double> polylineCoordinates =
          curves.SelectMany(c => PointListToFlatArray(new DS.Point[2] { c.StartPoint, c.EndPoint })).ToList();
        polylineCoordinates.AddRange(PointToArray(curves.Last().EndPoint));
        curves.ForEach(c => c.Dispose());

        Polyline displayValue = new Polyline(polylineCoordinates, ModelUnits);
        List<double> dsKnots = curve.Knots().ToList();
        dsKnots.RemoveAt(dsKnots.Count - 1);
        dsKnots.RemoveAt(0);

        Curve spkCurve = new Curve(displayValue, ModelUnits);
        spkCurve.weights = curve.Weights().ToList();
        spkCurve.points = PointListToFlatArray(curve.ControlPoints()).ToList();
        spkCurve.knots = dsKnots;
        spkCurve.degree = curve.Degree;
        spkCurve.periodic = curve.IsPeriodic;
        spkCurve.rational = curve.IsRational;
        spkCurve.closed = curve.IsClosed;
        spkCurve.domain = new Interval(curve.StartParameter(), curve.EndParameter());
        //spkCurve.Properties

        //spkCurve.GenerateHash();
        spkCurve.length = curve.Length;
        spkCurve.bbox = BoxToSpeckle(curve.BoundingBox.ToCuboid());
        
        speckleCurve = spkCurve;
      }

      CopyProperties(speckleCurve, curve);
      return speckleCurve;
    }

    public Base HelixToSpeckle(Helix helix)
    {
      using (NurbsCurve nurbsCurve = helix.ToNurbsCurve())
      {
        var curve = CurveToSpeckle(nurbsCurve);
        CopyProperties(curve, helix);
        return curve;
      }
    }

    #endregion

    #region mesh

    public DS.Mesh BrepToNative(Brep brep)
    {
      if (brep.displayValue != null)
      {
        var meshToNative = MeshToNative(brep.displayValue);
        return meshToNative;
      }

      return null;
    }

    // Meshes
    public Mesh MeshToSpeckle(DS.Mesh mesh)
    {
      var vertices = PointListToFlatArray(mesh.VertexPositions);
      var defaultColour = System.Drawing.Color.FromArgb(255, 100, 100, 100);

      var faces = mesh.FaceIndices.SelectMany(f =>
        {
          if (f.Count == 4)
          {
            return new int[5] { 1, (int)f.A, (int)f.B, (int)f.C, (int)f.D };
          }
          else
          {
            return new int[4] { 0, (int)f.A, (int)f.B, (int)f.C };
          }
        })
        .ToArray();

      var colors = Enumerable.Repeat(defaultColour.ToArgb(), vertices.Count()).ToArray();
      //double[] textureCoords;

      //if (SpeckleRhinoConverter.AddMeshTextureCoordinates)
      //{
      //  textureCoords = mesh.TextureCoordinates.Select(pt => pt).ToFlatArray();
      //  return new SpeckleMesh(vertices, faces, Colors, textureCoords, properties: mesh.UserDictionary.ToSpeckle());
      //}

      var speckleMesh = new Mesh(vertices, faces, colors, units: ModelUnits);

      CopyProperties(speckleMesh, mesh);
      
      // Todo: Cannot directly compute bounding box for a mesh.
      
      return speckleMesh;
    }

    public DS.Mesh MeshToNative(Mesh mesh)
    {
      var points = ArrayToPointList(mesh.vertices, mesh.units);
      List<IndexGroup> faces = new List<IndexGroup>();
      int i = 0;

      while (i < mesh.faces.Count)
      {
        if (mesh.faces[i] == 0)
        {
          // triangle
          var ig = IndexGroup.ByIndices((uint)mesh.faces[i + 1], (uint)mesh.faces[i + 2], (uint)mesh.faces[i + 3]);
          faces.Add(ig);
          i += 4;
        }
        else
        {
          // quad
          var ig = IndexGroup.ByIndices((uint)mesh.faces[i + 1], (uint)mesh.faces[i + 2], (uint)mesh.faces[i + 3],
            (uint)mesh.faces[i + 4]);
          faces.Add(ig);
          i += 5;
        }
      }

      var dsMesh = DS.Mesh.ByPointsFaceIndices(points, faces);

      dsMesh.SetDynamoProperties<DS.Mesh>(GetDynamicMembersFromBase(mesh));

      return dsMesh;
    }

    #endregion


    public Cuboid BoxToNative(Box box)
    {
      using (var coordinateSystem = PlaneToNative(box.basePlane).ToCoordinateSystem())
        using (var cLow = DS.Point.ByCartesianCoordinates(coordinateSystem, box.xSize.start ?? 0, box.ySize.start ?? 0, box.zSize.start ?? 0))
          using (var cHigh = DS.Point.ByCartesianCoordinates(coordinateSystem, box.xSize.end ?? 0, box.ySize.end ?? 0, box.zSize.end ?? 0))
            return Cuboid.ByCorners(cLow, cHigh);
    }

    public Box BoxToSpeckle(Cuboid box)
    {
      var plane = PlaneToSpeckle(box.ContextCoordinateSystem.XYPlane);
      
      // Todo: Check for cubes that are offset from the plane origin to ensure correct positioning.
      return new Box(plane, new Interval(-box.Width/2, box.Width/2), new Interval(-box.Length/2, box.Length/2), new Interval(-box.Height/2, box.Height/2));
    }
    
    public NurbsSurface SurfaceToNative(Surface surface)
    {
      var points = new DS.Point[][] { };
      var weights = new double[][] { };

      var controlPoints = surface.GetControlPoints();

      points = controlPoints.Select(row => row.Select(p
        => DS.Point.ByCoordinates(
        ScaleToNative(p.x, p.units),
          ScaleToNative(p.y, p.units),
          ScaleToNative(p.z, p.units))).ToArray())
        .ToArray();

      weights = controlPoints.Select(row => row.Select(p => p.weight).ToArray()).ToArray();

      var knotsU = surface.knotsU;
      knotsU.Insert(0, knotsU[0]);
      knotsU.Add(knotsU[knotsU.Count - 1]);

      var knotsV = surface.knotsV;
      knotsV.Insert(0, knotsV[0]);
      knotsV.Add(knotsV[knotsV.Count - 1]);

      var result = DS.NurbsSurface.ByControlPointsWeightsKnots(points, weights, knotsU.ToArray(), surface.knotsV.ToArray(),
        surface.degreeU, surface.degreeV);
      return result;
    }

    public Surface SurfaceToSpeckle(NurbsSurface surface)
    {
      var result = new Surface();
      result.units = ModelUnits;
      // Set control points
      var dsPoints = surface.ControlPoints();
      var dsWeights = surface.Weights();
      var points = new List<List<ControlPoint>>();
      for (var i = 0; i < dsPoints.Length; i++)
      {
        var row = new List<ControlPoint>();
        for (var j = 0; j < dsPoints[i].Length; j++)
        {
          var dsPoint = dsPoints[i][j];
          var dsWeight = dsWeights[i][j];
          row.Add(new ControlPoint(dsPoint.X, dsPoint.Y, dsPoint.Z, dsWeight, null));
        }

        points.Add(row);
      }

      result.SetControlPoints(points);

      // Set degree
      result.degreeU = surface.DegreeU;
      result.degreeV = surface.DegreeV;

      // Set knot vectors
      result.knotsU = surface.UKnots().ToList();
      result.knotsV = surface.VKnots().ToList();

      // Set other
      result.rational = surface.IsRational;
      result.closedU = surface.ClosedInU;
      result.closedV = surface.ClosedInV;

      result.area = surface.Area;
      result.bbox = BoxToSpeckle(surface.BoundingBox.ToCuboid());
      
      return result;
    }

    /// <summary>
    /// Copies props from a design script entity to a speckle object.
    /// </summary>
    /// <param name="to"></param>
    /// <param name="from"></param>
    public void CopyProperties(Base to, DesignScriptEntity from)
    {
      var dict = from.GetSpeckleProperties();
      foreach(var kvp in dict)
      {
        to[kvp.Key] = kvp.Value;
      }
    }

    public Dictionary<string, object> GetDynamicMembersFromBase(Base obj)
    {
      var dict = new Dictionary<string, object>();
      foreach( var prop in obj.GetDynamicMembers())
      {
        dict.Add(prop, obj[prop]);
      }
      return dict;
    }

  }
}