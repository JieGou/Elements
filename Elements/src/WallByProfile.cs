using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements
{
    /// <summary>
    /// A wall drawn using the elevation profile
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(Elements.Serialization.JSON.JsonInheritanceConverter), "discriminator")]
    public class WallByProfile : Wall
    {
        /// <summary>The Profile, which includes Openings that will be extruded.</summary>
        [Newtonsoft.Json.JsonProperty("Profile", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Obsolete("The Profile property is obsolete, use the GetProfile method to access a profile created from the perimeter and the openings.")]
        public new Profile Profile { get; set; }

        /// <summary>The overall thickness of the Wall</summary>
        [Newtonsoft.Json.JsonProperty("Thickness", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double Thickness { get; set; }

        /// <summary>
        /// The perimeter of the Wall's elevation.  It is assumed to be in the same Plane as the Centerline,
        /// and will often be projected to that Plane during internal operations.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("Perimeter", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Polygon Perimeter { get; set; }

        /// <summary>The Centerline of the wall</summary>
        [Newtonsoft.Json.JsonProperty("Centerline", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Line Centerline { get; set; }

        /// <summary>
        /// Construct a wall by profile.
        /// </summary>
        /// <param name="perimeter">The perimeter of the wall elevation.</param>
        /// <param name="profile">This value should be left null.</param>
        /// <param name="thickness">The thickness of the wall.</param>
        /// <param name="centerline">The centerline of the wall.</param>
        /// <param name="transform">The transform of the wall.</param>
        /// <param name="material">The material of the wall.</param>
        /// <param name="representation">The representation of the wall.</param>
        /// <param name="isElementDefinition">Is the wall an element definition?</param>
        /// <param name="id">The id of the wall.</param>
        /// <param name="name">The name of the wall.</param>
        [Newtonsoft.Json.JsonConstructor]
        [Obsolete("Do not use.  This constructor is only preserved to maintain backwards compatibility upon serialization/deserialization.")]
        public WallByProfile(Polygon @perimeter,
                             Profile @profile,
                             double @thickness,
                             Line @centerline,
                             Transform @transform,
                             Material @material,
                             List<Opening> @openings,
                             Representation @representation,
                             bool @isElementDefinition,
                             System.Guid @id,
                             string @name)
            : base(transform, material, representation, isElementDefinition, id, name)
        {
            this.Perimeter = @perimeter;
            this.Profile = @profile;
            this.Thickness = @thickness;
            this.Centerline = @centerline;
            this.Openings.AddRange(@openings);
        }

        /// <summary>
        /// The Profile of the Wall computed from its Perimeter and the Openings.
        /// </summary>
        /// <returns></returns>
        public Profile GetProfile()
        {
            return new Profile(Perimeter, Openings.Select(o => o.Perimeter).ToList());
        }

        /// <summary>
        /// The computed height of the Wall.
        /// </summary>
        public double GetHeight()
        {
            var bottom = Math.Min(Centerline.Start.Z, Centerline.End.Z);
            var top = Perimeter.Vertices.Max(v => v.Z);
            return top - bottom;
        }

        /// <summary>
        /// Create a Wall from a Profile and thickness.  If centerline is not included it will be
        /// computed from the Profile.  The Profile will be projected to the
        /// centerline Plane, and used to find Openings of the Wall.
        /// </summary>
        public WallByProfile(Profile @profile,
                             double @thickness,
                             Line @centerline = null,
                             Transform @transform = null,
                             Material @material = null,
                             Representation @representation = null,
                             bool @isElementDefinition = false)
            : base(transform != null ? transform : new Transform(),
                   material != null ? material : BuiltInMaterials.Concrete,
                   representation != null ? representation : new Representation(new List<SolidOperation>()),
                   isElementDefinition,
                   Guid.NewGuid(),
                   "Wall by Profile")
        {
            var point = profile.Perimeter.Vertices.First();
            var centerPlane = new Plane(centerline.Start, centerline.End, centerline.End + Vector3.ZAxis);
            this.Perimeter = profile.Perimeter.Project(centerPlane);

            var perpendicularToWall = centerline.Direction().Cross(Vector3.ZAxis);
            foreach (var v in profile.Voids)
            {
                var opening = new Opening(v, perpendicularToWall, 1.1 * thickness, 1.1 * thickness);
                this.Openings.Add(opening);
            }

            this.Thickness = @thickness;
            this.Centerline = @centerline;
        }

        /// <summary>Update the geometric representation of this Wall.</summary>
        public override void UpdateRepresentations()
        {
            this.Representation.SolidOperations.Clear();

            if (this.Profile != null)
            {
                //TODO remove this geometry path once we completely delete the obsolete Profile property.
                // To ensure the correct direction, we find the direction from a point on the Polygon to the vertical plane of the Centerline
                var point = Profile.Perimeter.Vertices.First();
                var centerPlane = new Plane(Centerline.Start, Centerline.End, Centerline.End + Vector3.ZAxis);
                var direction = new Line(point, point.Project(centerPlane)).Direction();

                this.Representation.SolidOperations.Add(new Extrude(this.Profile, this.Thickness, direction, false));
            }
            else
            {
                var direction = Centerline.Direction().Cross(Vector3.ZAxis);
                var shiftedProfile = GetProfile().Transformed(new Transform(direction.Negate() * Thickness / 2));

                this.Representation.SolidOperations.Add(new Extrude(shiftedProfile, this.Thickness, direction, false));
            }
        }
    }
}