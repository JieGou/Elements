using System.Collections.Generic;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Serialization.glTF;
using Elements.Utilities;
using glTFLoader.Schema;

namespace Elements
{
    /// <summary>
    /// Represents an element as a reference to a GLB file location within the content catalog.
    /// </summary>
    public class ContentRepresentation : ElementRepresentation
    {
        /// <summary>The URI of the glb for this element.</summary>
        public string GlbLocation { get; set; }

        /// <summary>The bounding box of the content.</summary>
        public BBox3 BoundingBox { get; set; }

        /// <summary>The scale needed to convert the glb to meters.</summary>
        public double GlbScaleToMeters { get; set; }

        /// <summary>A vector indicating the direction the source object was originally facing.</summary>
        public Vector3 SourceDirection { get; set; }

        /// <summary>
        /// Initializes a new instance of ContentRepresentation class
        /// </summary>
        /// <param name="boundingBox">The bounding box of the content.</param>
        /// <param name="glbLocation">The URI of the glb for this element.</param>
        /// <param name="glbScaleToMeters">The scale needed to convert the glb to meters.</param>
        /// <param name="sourceDirection">A vector indicating the direction the source object was originally facing.</param>
        public ContentRepresentation(BBox3 boundingBox, string glbLocation, double glbScaleToMeters, Vector3 sourceDirection)
        {
            GlbLocation = glbLocation;
            BoundingBox = boundingBox;
            GlbScaleToMeters = glbScaleToMeters;
            SourceDirection = sourceDirection;
        }

        /// <inheritdoc/>
        public override bool TryToGraphicsBuffers(GeometricElement element, out List<GraphicsBuffers> graphicsBuffers, out string id, out MeshPrimitive.ModeEnum? mode)
        {
            id = element.Id + "_mesh";

            graphicsBuffers = new List<GraphicsBuffers>();
            mode = MeshPrimitive.ModeEnum.TRIANGLES;

            if (!BoundingBox.IsValid() || BoundingBox.IsDegenerate())
            {
                return false;
            }

            var bottomProfile = new Geometry.Polygon(new List<Vector3>{
                            new Vector3(BoundingBox.Min.X - 2, BoundingBox.Min.Y, BoundingBox.Min.Z + 3),
                            new Vector3(BoundingBox.Min.X - 2, BoundingBox.Max.Y, BoundingBox.Min.Z + 3),
                            new Vector3(BoundingBox.Max.X - 2, BoundingBox.Max.Y, BoundingBox.Min.Z + 3),
                            new Vector3(BoundingBox.Max.X - 2, BoundingBox.Min.Y, BoundingBox.Min.Z + 3),
                        });

            var height = BoundingBox.Max.Z - BoundingBox.Min.Z;
            var boxSolid = new Extrude(bottomProfile, height, Vector3.ZAxis, false);

            var csg = SolidOperationUtils.GetFinalCsgFromSolids(new List<SolidOperation>() { boxSolid }, element, false);

            if (csg == null)
            {
                return false;
            }

            GraphicsBuffers buffers = null;
            buffers = csg.Tessellate();

            if (buffers.Vertices.Count == 0)
            {
                return false;
            }

            graphicsBuffers.Add(buffers);
            return true;
        }

        internal override List<NodeExtension> GetNodeExtensions(GeometricElement element)
        {
            var extensions = base.GetNodeExtensions(element);
            extensions.Add(new NodeExtension("HYPAR_referenced_content", "contentUrl", GlbLocation));
            return extensions;
        }
    }
}