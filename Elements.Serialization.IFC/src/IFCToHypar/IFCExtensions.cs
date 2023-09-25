using System;
using System.Collections.Generic;
using System.Linq;
using Elements.BuildingElements;
using Elements.Geometry;
using Elements.Geometry.Interfaces;
using Elements.Geometry.Solids;
using IFC;

namespace Elements.Serialization.IFC.IFCToHypar
{
    /// <summary>
    /// Extension methods for converting IFC entities to elements.
    /// </summary>
    internal static class IFCExtensions
    {
        internal static Representation GetRepresentationFromProduct(this IfcProduct product,
                                                                   List<string> constructionErrors,
                                                                   Dictionary<Guid, Material> repMaterialMap,
                                                                   out Transform mapTransform,
                                                                   out Guid mapId,
                                                                   out Material materialHint)
        {
            if (product.Representation == null)
            {
                mapTransform = null;
                materialHint = null;
                return null;
            }
            var repItems = product.Representation.Representations.SelectMany(r => r.Items);
            var repMap = new Dictionary<Guid, List<SolidOperation>>();
            var ops = ParseRepresentationItems(repItems,
                                               constructionErrors,
                                               repMap,
                                               repMaterialMap,
                                               out mapTransform,
                                               out mapId,
                                               out materialHint);
            return new Representation(ops);
        }

        private static List<SolidOperation> ParseRepresentationItems(IEnumerable<IfcRepresentationItem> repItems,
                                                                     List<string> constructionErrors,
                                                                     Dictionary<Guid, List<SolidOperation>> repMap,
                                                                     Dictionary<Guid, Material> repMaterialMap,
                                                                     out Transform mapTransform,
                                                                     out Guid mapId,
                                                                     out Material materialHint)
        {
            var solidOps = new List<SolidOperation>();
            mapTransform = null;
            materialHint = null;

            foreach (var item in repItems)
            {
                if (repMaterialMap.ContainsKey(item.Id))
                {
                    materialHint = repMaterialMap[item.Id];
                }

                var notImplementedException = $"{item.GetType().Name} is not yet supported.";

                if (item is IfcConnectedFaceSet)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcEdge)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcFace)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcFaceBound)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcPath)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcVertex)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcLoop)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcFacetedBrep)
                {
                    var ifcSolid = (IfcFacetedBrep)item;
                    var shell = ifcSolid.Outer;
                    var newSolid = new Solid();
                    for (var i = 0; i < shell.CfsFaces.Count; i++)
                    {
                        var f = shell.CfsFaces[i];
                        foreach (var b in f.Bounds)
                        {
                            var loop = (IfcPolyLoop)b.Bound;
                            var poly = loop.Polygon.ToPolygon();
                            newSolid.AddFace(poly);
                        }
                    }
                    solidOps.Add(new ImportSolid(newSolid));
                }
                else if (item is IfcFacetedBrepWithVoids)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcExtrudedAreaSolid)
                {
                    var ifcSolid = (IfcExtrudedAreaSolid)item;
                    var profile = ifcSolid.SweptArea.ToProfile();
                    var solidTransform = ifcSolid.Position.ToTransform();
                    var direction = ifcSolid.ExtrudedDirection.ToVector3();

                    if (profile == null)
                    {
                        throw new NotImplementedException($"{profile.GetType().Name} is not supported for IfcExtrudedAreaSolid.");
                    }
                    var extrude = new Extrude(solidTransform.OfProfile(profile),
                                                (IfcLengthMeasure)ifcSolid.Depth,
                                                solidTransform.OfVector(direction).Unitized(),
                                                false);
                    solidOps.Add(extrude);
                }
                else if (item is IfcRevolvedAreaSolid)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcSurfaceCurveSweptAreaSolid)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcCsgSolid)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcSweptDiskSolid)
                {
                    constructionErrors.Add(notImplementedException);
                }
                else if (item is IfcMappedItem)
                {
                    var mappedItem = (IfcMappedItem)item;

                    if (repMap.ContainsKey(mappedItem.MappingSource.MappedRepresentation.Id))
                    {
                        var ops = repMap[mappedItem.MappingSource.MappedRepresentation.Id];
                        solidOps.AddRange(ops);
                    }
                    else
                    {
                        var ops = ParseRepresentationItems(mappedItem.MappingSource.MappedRepresentation.Items,
                                                       constructionErrors,
                                                       repMap,
                                                       repMaterialMap,
                                                       out mapTransform,
                                                       out mapId,
                                                       out materialHint);
                        repMap.Add(mappedItem.MappingSource.MappedRepresentation.Id, ops);
                        solidOps.AddRange(ops);
                    }

                    mapId = mappedItem.MappingSource.MappedRepresentation.Id;
                    mapTransform = mappedItem.MappingTarget.ToTransform();
                }
                else if (item is IfcStyledItem)
                {
                    constructionErrors.Add(notImplementedException);
                }
            }

            return solidOps;
        }

        internal static Transform GetTransformFromIfcElement(IfcElement ifcElement)
        {
            // TODO: AC20-Institute-Var-2.ifc model contains doors with IfcFacetedBrep based representation.
            var repItems = ifcElement.Representation.Representations.SelectMany(r => r.Items);
            if (!repItems.Any())
            {
                throw new Exception("The provided IfcDoor does not have any representations.");
            }

            var containedInStructureTransform = new Transform();
            containedInStructureTransform.Concatenate(ifcElement.ObjectPlacement.ToTransform());

            // Check if the door is contained in a building storey
            foreach (var cis in ifcElement.ContainedInStructure)
            {
                containedInStructureTransform.Concatenate(cis.RelatingStructure.ObjectPlacement.ToTransform());
            }

            var repMappedItems = repItems.OfType<IfcMappedItem>();

            if (repMappedItems.Any())
            {
                var representation = repMappedItems.FirstOrDefault();
                var localOrigin = representation.MappingTarget.LocalOrigin.ToVector3();
                return new Transform(localOrigin).Concatenated(containedInStructureTransform);
            }

            var repSolidItems = repItems.OfType<IfcExtrudedAreaSolid>();

            if (repSolidItems.Any())
            {
                var representation = repSolidItems.FirstOrDefault();
                var solidTransform = representation.Position.ToTransform();
                return solidTransform.Concatenated(containedInStructureTransform);
            }

            return containedInStructureTransform;
        }

        internal static DoorOpeningSide GetDoorOpeningSide(this IfcDoor ifcDoor)
        {
            switch (ifcDoor.OperationType)
            {
                case IfcDoorTypeOperationEnum.SINGLE_SWING_LEFT:
                case IfcDoorTypeOperationEnum.DOUBLE_SWING_LEFT:
                    return DoorOpeningSide.LeftHand;
                case IfcDoorTypeOperationEnum.SINGLE_SWING_RIGHT:
                case IfcDoorTypeOperationEnum.DOUBLE_SWING_RIGHT:
                    return DoorOpeningSide.RightHand;
                case IfcDoorTypeOperationEnum.DOUBLE_DOOR_SINGLE_SWING:
                case IfcDoorTypeOperationEnum.DOUBLE_DOOR_DOUBLE_SWING:
                    return DoorOpeningSide.DoubleDoor;
            }
            return DoorOpeningSide.Undefined;
        }

        internal static DoorOpeningType GetDoorOpeningType(this IfcDoor ifcDoor)
        {
            switch (ifcDoor.OperationType)
            {
                case IfcDoorTypeOperationEnum.SINGLE_SWING_LEFT:
                case IfcDoorTypeOperationEnum.SINGLE_SWING_RIGHT:
                case IfcDoorTypeOperationEnum.DOUBLE_DOOR_SINGLE_SWING:
                    return DoorOpeningType.SingleSwing;
                case IfcDoorTypeOperationEnum.DOUBLE_SWING_LEFT:
                case IfcDoorTypeOperationEnum.DOUBLE_SWING_RIGHT:
                case IfcDoorTypeOperationEnum.DOUBLE_DOOR_DOUBLE_SWING:
                    return DoorOpeningType.DoubleSwing;
            }
            return DoorOpeningType.Undefined;
        }

        internal static Opening ToOpening(this IfcOpeningElement opening)
        {
            var openingTransform = opening.ObjectPlacement.ToTransform();
            var s = opening.RepresentationsOfType<IfcExtrudedAreaSolid>().FirstOrDefault();
            if (s != null)
            {
                var solidTransform = s.Position.ToTransform();
                solidTransform.Concatenate(openingTransform);
                var profile = (Polygon)s.SweptArea.ToCurve();

                var newOpening = new Opening(profile,
                                             default,
                                             (IfcLengthMeasure)s.Depth,
                                             (IfcLengthMeasure)s.Depth,
                                             solidTransform,
                                             null,
                                             false,
                                             IfcGuid.FromIfcGUID(opening.GlobalId));
                return newOpening;
            }
            return null;
        }

        private static Solid Representations(this IfcProduct product)
        {
            var reps = product.Representation.Representations.SelectMany(r => r.Items);
            foreach (var r in reps)
            {
                if (r is IfcSurfaceCurveSweptAreaSolid)
                {
                    throw new Exception("IfcSurfaceCurveSweptAreaSolid is not supported yet.");
                }
                if (r is IfcRevolvedAreaSolid)
                {
                    throw new Exception("IfcRevolvedAreaSolid is not supported yet.");
                }
                if (r is IfcSweptDiskSolid)
                {
                    throw new Exception("IfcSweptDiskSolid is not supported yet.");
                }
                else if (r is IfcExtrudedAreaSolid eas)
                {
                    var profileDef = (IfcArbitraryClosedProfileDef)eas.SweptArea;
                    var pline = (IfcPolyline)profileDef.OuterCurve;
                    var outline = pline.ToPolygon(true);
                    var solid = Solid.SweepFace(outline, null, (IfcLengthMeasure)eas.Depth);
                    return solid;
                }
                else if (r is IfcFacetedBrep brep)
                {
                    var solid = new Solid();
                    var fbr = brep;
                    var shell = fbr.Outer;
                    var faces = new Face[shell.CfsFaces.Count];
                    for (var i = 0; i < shell.CfsFaces.Count; i++)
                    {
                        var f = shell.CfsFaces[i];
                        var boundCount = 0;
                        Loop outer = null;
                        Loop[] inner = new Loop[f.Bounds.Count - 1];
                        foreach (var b in f.Bounds)
                        {
                            var loop = (IfcPolyLoop)b.Bound;
                            var newLoop = loop.Polygon.ToLoop(solid);
                            if (boundCount == 0)
                            {
                                outer = newLoop;
                            }
                            else
                            {
                                inner[boundCount - 1] = newLoop;
                            }
                            boundCount++;
                        }
                        solid.AddFace(outer, inner);
                    }
                    return solid;
                }
                else if (r is IfcFacetedBrepWithVoids)
                {
                    throw new Exception("IfcFacetedBrepWithVoids is not supported yet.");
                }
            }
            return null;
        }

        internal static IEnumerable<T> RepresentationsOfType<T>(this IfcProduct product) where T : IfcGeometricRepresentationItem
        {
            var reps = product.Representation.Representations.SelectMany(r => r.Items);
            if (reps.Any())
            {
                return reps.OfType<T>();
            }
            return null;
        }

        // private static IfcOpeningElement ToIfcOpeningElement(this Opening opening, IfcRepresentationContext context, Document doc, IfcObjectPlacement parent)
        // {
        //     // var sweptArea = opening.Profile.Perimeter.ToIfcArbitraryClosedProfileDef(doc);
        //     // We use the Z extrude direction because the direction is
        //     // relative to the local placement, which is a transform at the
        //     // beam's end with the Z axis pointing along the direction.

        //     // var extrudeDirection = opening.ExtrudeDirection.ToIfcDirection();
        //     // var position = new Transform().ToIfcAxis2Placement3D(doc);
        //     // var solid = new IfcExtrudedAreaSolid(sweptArea, position,
        //     //     extrudeDirection, new IfcPositiveLengthMeasure(opening.ExtrudeDepth));

        //     var extrude= (Extrude)opening.Geometry.SolidOperations[0];
        //     var solid = extrude.ToIfcExtrudedAreaSolid(new Transform(), doc);
        //     var localPlacement = new Transform().ToIfcLocalPlacement(doc, parent);

        //     var shape = new IfcShapeRepresentation(context, "Body", "SweptSolid", new List<IfcRepresentationItem>{solid});
        //     var productRep = new IfcProductDefinitionShape(new List<IfcRepresentation>{shape});

        //     var ifcOpening = new IfcOpeningElement(IfcGuid.ToIfcGuid(opening.Id), null, null, null, null, localPlacement, productRep, null);

        //     // doc.AddEntity(sweptArea);
        //     // doc.AddEntity(extrudeDirection);
        //     // doc.AddEntity(position);
        //     // doc.AddEntity(repItem);

        //     doc.AddEntity(solid);
        //     doc.AddEntity(localPlacement);
        //     doc.AddEntity(shape);
        //     doc.AddEntity(productRep);

        //     return ifcOpening;
        // }

        internal static ICurve ToCurve(this IfcProfileDef profile)
        {
            if (profile is IfcCircleProfileDef cpd)
            {
                // TODO: Remove this conversion to a polygon when downstream
                // functions support arcs and circles.
                return new Circle((IfcLengthMeasure)cpd.Radius).ToPolygon(10);
            }
            else if (profile is IfcParameterizedProfileDef ipd)
            {
                return ipd.ToCurve();
            }
            else if (profile is IfcArbitraryOpenProfileDef aopd)
            {
                return aopd.ToCurve();
            }
            else if (profile is IfcArbitraryClosedProfileDef acpd)
            {
                return acpd.ToCurve();
            }
            else if (profile is IfcCompositeProfileDef)
            {
                throw new Exception("IfcCompositeProfileDef is not supported yet.");
            }
            else if (profile is IfcDerivedProfileDef)
            {
                throw new Exception("IfcDerivedProfileDef is not supported yet.");
            }
            return null;
        }

        internal static ICurve ToCurve(this IfcParameterizedProfileDef profile)
        {
            if (profile is IfcRectangleProfileDef rect)
            {
                var p = Polygon.Rectangle((IfcLengthMeasure)rect.XDim, (IfcLengthMeasure)rect.YDim);
                var t = new Transform(rect.Position.Location.ToVector3());
                return p.Transformed(t);
            }
            else if (profile is IfcCircleProfileDef circle)
            {
                return new Circle((IfcLengthMeasure)circle.Radius);
            }
            else
            {
                throw new Exception($"The IfcParameterizedProfileDef type, {profile.GetType().Name}, is not supported.");
            }
        }

        internal static ICurve ToCurve(this IfcArbitraryOpenProfileDef profile)
        {
            return profile.Curve.ToCurve(false);
        }

        internal static ICurve ToCurve(this IfcArbitraryClosedProfileDef profile)
        {
            return profile.OuterCurve.ToCurve(true);
        }

        internal static ICurve ToCurve(this IfcCurve curve, bool closed)
        {
            if (curve is IfcBoundedCurve)
            {
                if (curve is IfcCompositeCurve)
                {
                    throw new Exception("IfcCompositeCurve is not supported yet.");
                }
                else if (curve is IfcPolyline pl)
                {
                    if (closed)
                    {
                        return pl.ToPolygon(true);
                    }
                    else
                    {
                        return pl.ToPolyline();
                    }
                }
                else if (curve is IfcTrimmedCurve)
                {
                    throw new Exception("IfcTrimmedCurve is not supported yet.");
                }
                else if (curve is IfcBSplineCurve)
                {
                    throw new Exception("IfcBSplineCurve is not supported yet.");
                }
            }
            else if (curve is IfcConic)
            {
                throw new Exception("IfcConic is not supported yet.");
            }
            else if (curve is IfcOffsetCurve2D)
            {
                throw new Exception("IfcOffsetCurve2D is not supported yet.");
            }
            else if (curve is IfcOffsetCurve3D)
            {
                throw new Exception("IfcOffsetCurve3D is not supported yet.");
            }
            return null;
        }

        internal static Profile ToProfile(this IfcProfileDef profile)
        {
            Polygon outer = null;
            List<Polygon> inner = new List<Polygon>();

            if (profile is IfcRectangleProfileDef rect)
            {
                var p = Polygon.Rectangle((IfcLengthMeasure)rect.XDim, (IfcLengthMeasure)rect.YDim);
                var t = new Transform(rect.Position.Location.ToVector3());
                outer = (Polygon)p.Transformed(t);
            }
            else if (profile is IfcCircleProfileDef circle)
            {
                outer = new Circle((IfcLengthMeasure)circle.Radius).ToPolygon();
            }
            else if (profile is IfcArbitraryClosedProfileDef closedProfile)
            {
                var outerCurve = closedProfile.OuterCurve.ToCurve(true);
                if (outerCurve is Polygon pc)
                {
                    outer = pc;
                }
                else if (outerCurve is IndexedPolycurve ipc)
                {
                    outer = ipc.ToPolygon();
                }

                if (profile is IfcArbitraryProfileDefWithVoids)
                {
                    var voidProfile = (IfcArbitraryProfileDefWithVoids)profile;
                    inner.AddRange(voidProfile.InnerCurves.Select(c =>
                    {
                        var elCurve = c.ToCurve(true);
                        if (elCurve is Polygon voidP)
                        {
                            return voidP;
                        }
                        else if (elCurve is IndexedPolycurve voidPc)
                        {
                            return voidPc.ToPolygon();
                        }
                        return null;
                    }));
                }
            }
            else
            {
                throw new Exception($"The profile type, {profile.GetType().Name}, is not supported.");
            }

            // var name = profile.ProfileName == null ? null : profile.ProfileName;
            var newProfile = new Profile(outer, inner, profile.Id, string.Empty);
            return newProfile;
        }

        internal static Vector3 ToVector3(this IfcCartesianPoint cartesianPoint)
        {
            return cartesianPoint.Coordinates.ToVector3();
        }

        internal static Vector3 ToVector3(this List<IfcLengthMeasure> measures)
        {
            if (measures.Count == 2)
            {
                return new Vector3(measures[0], measures[1]);
            }
            else if (measures.Count == 3)
            {
                return new Vector3(measures[0], measures[1], measures[2]);
            }
            else
            {
                throw new Exception($"{measures.Count} measures could not be converted to a Vector3.");
            }
        }

        internal static Polygon ToPolygon(this IfcPolyline polyline, bool dropLastPoint = false)
        {
            var count = dropLastPoint ? polyline.Points.Count - 1 : polyline.Points.Count;
            var verts = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var v = polyline.Points[i].ToVector3();
                verts[i] = v;
            }
            return new Polygon(verts);
        }

        internal static Polyline ToPolyline(this IfcPolyline polyline)
        {
            var verts = polyline.Points.Select(p => p.ToVector3()).ToArray();
            return new Polyline(verts);
        }

        internal static bool IsClosed(this IfcPolyline pline)
        {
            var start = pline.Points[0];
            var end = pline.Points[pline.Points.Count - 1];
            return start.Equals(end);
        }

        internal static bool Equals(this IfcCartesianPoint point, IfcCartesianPoint other)
        {
            for (var i = 0; i < point.Coordinates.Count; i++)
            {
                if (point.Coordinates[i] != other.Coordinates[i])
                {
                    return false;
                }
            }
            return true;
        }
        internal static Transform ToTransform(this IfcAxis2Placement3D cs)
        {
            var x = cs.RefDirection != null ? cs.RefDirection.ToVector3() : Vector3.XAxis;
            var z = cs.Axis != null ? cs.Axis.ToVector3() : Vector3.ZAxis;
            var y = z.Cross(x);
            var o = cs.Location.ToVector3();
            var t = new Transform(o, x, y, z);
            return t;
        }

        internal static Transform ToTransform(this IfcAxis2Placement2D cs)
        {
            var d = cs.RefDirection.ToVector3();
            var z = Vector3.ZAxis;
            var o = cs.Location.ToVector3();
            return new Transform(o, d, z);
        }

        internal static Vector3 ToVector3(this IfcDirection direction)
        {
            var ratios = direction.DirectionRatios;
            return new Vector3(ratios[0], ratios[1], ratios[2]);
        }

        internal static Transform ToTransform(this IfcAxis2Placement placement)
        {
            // SELECT IfcAxis2Placement3d, IfcAxis2Placement2d
            if (placement.Choice.GetType() == typeof(IfcAxis2Placement2D))
            {
                var cs = (IfcAxis2Placement2D)placement.Choice;
                return cs.ToTransform();
            }
            else if (placement.Choice.GetType() == typeof(IfcAxis2Placement3D))
            {
                var cs = (IfcAxis2Placement3D)placement.Choice;
                var t = cs.ToTransform();
                return t;
            }
            else
            {
                throw new Exception($"The specified placement of type, {placement.GetType().ToString()}, cannot be converted to a Transform.");
            }
        }

        internal static Transform ToTransform(this IfcLocalPlacement placement)
        {
            var t = placement.RelativePlacement.ToTransform();
            if (placement.PlacementRelTo != null)
            {
                var tr = placement.PlacementRelTo.ToTransform();
                t.Concatenate(tr);
            }
            return t;
        }

        internal static Transform ToTransform(this IfcObjectPlacement placement)
        {
            if (placement.GetType() == typeof(IfcLocalPlacement))
            {
                var lp = (IfcLocalPlacement)placement;
                var t = lp.ToTransform();
                return t;
            }
            else if (placement.GetType() == typeof(IfcGridPlacement))
            {
                throw new Exception("IfcGridPlacement conversion to Transform not supported.");
            }
            return null;
        }

        internal static Transform ToTransform(this IfcCartesianTransformationOperator op)
        {
            if (op is IfcCartesianTransformationOperator2D)
            {
                var op2D = (IfcCartesianTransformationOperator2D)op;
                return op2D.ToTransform();
            }
            else if (op is IfcCartesianTransformationOperator3D)
            {
                var op3D = (IfcCartesianTransformationOperator3D)op;
                return op3D.ToTransform();
            }
            return null;
        }

        internal static Transform ToTransform(this IfcCartesianTransformationOperator2D op)
        {
            var o = op.LocalOrigin.ToVector3();
            var x = op.Axis1 == null ? Vector3.XAxis : op.Axis1.ToVector3().Unitized();
            var y = op.Axis2 == null ? Vector3.YAxis : op.Axis2.ToVector3().Unitized();
            var z = x.Cross(y);
            return new Transform(o, x, y, z);
        }

        internal static Transform ToTransform(this IfcCartesianTransformationOperator3D op)
        {
            var o = op.LocalOrigin.ToVector3();
            var x = op.Axis1 == null ? Vector3.XAxis : op.Axis1.ToVector3().Unitized();
            var y = op.Axis2 == null ? Vector3.YAxis : op.Axis2.ToVector3().Unitized();
            var z = op.Axis3 == null ? Vector3.ZAxis : op.Axis3.ToVector3().Unitized();
            return new Transform(o, x, y, z);
        }

        internal static Polygon ToPolygon(this List<IfcCartesianPoint> loop)
        {
            var verts = new Vector3[loop.Count];
            for (var i = 0; i < loop.Count; i++)
            {
                verts[i] = loop[i].ToVector3();
            }
            return new Polygon(verts);
        }

        internal static Loop ToLoop(this List<IfcCartesianPoint> loop, Solid solid)
        {
            var hes = new HalfEdge[loop.Count];
            for (var i = 0; i < loop.Count; i++)
            {
                var v = solid.AddVertex(loop[i].ToVector3());
                hes[i] = new HalfEdge(v);
            }
            var newLoop = new Loop(hes);
            return newLoop;
        }

        internal static Polygon ToPolygon(this IfcPolyLoop loop)
        {
            return loop.Polygon.ToPolygon();
        }
        internal static Color ToColor(this IfcColourRgb rgb, double transparency)
        {
            return new Color((IfcRatioMeasure)rgb.Red, (IfcRatioMeasure)rgb.Green, (IfcRatioMeasure)rgb.Blue, transparency);
        }
    }
}