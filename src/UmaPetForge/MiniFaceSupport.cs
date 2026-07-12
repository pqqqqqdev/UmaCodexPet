using System;
using System.Globalization;
using UnityEngine;

namespace UmaPetForge
{
    internal sealed class MiniFaceSelection : IEquatable<MiniFaceSelection>
    {
        public const int EyeMinimum = 0;
        public const int EyeMaximum = 14;
        public const int MouthMinimum = 0;
        public const int MouthMaximum = 18;
        public const int EyebrowMinimum = 0;
        public const int EyebrowMaximum = 8;

        public int EyeLeft { get; set; }
        public int EyeRight { get; set; }
        public int Mouth { get; set; }
        public int EyebrowLeft { get; set; }
        public int EyebrowRight { get; set; }

        public MiniFaceSelection()
            : this(0, 0, 0, 0, 0)
        {
        }

        public MiniFaceSelection(
            int eyeLeft,
            int eyeRight,
            int mouth,
            int eyebrowLeft,
            int eyebrowRight)
        {
            EyeLeft = eyeLeft;
            EyeRight = eyeRight;
            Mouth = mouth;
            EyebrowLeft = eyebrowLeft;
            EyebrowRight = eyebrowRight;
        }

        public MiniFaceSelection Clone()
        {
            return new MiniFaceSelection(
                EyeLeft,
                EyeRight,
                Mouth,
                EyebrowLeft,
                EyebrowRight);
        }

        public bool TryValidate(out string error)
        {
            if (!IsInRange(EyeLeft, EyeMinimum, EyeMaximum))
            {
                error = RangeError("left eye", EyeLeft, EyeMinimum, EyeMaximum);
                return false;
            }

            if (!IsInRange(EyeRight, EyeMinimum, EyeMaximum))
            {
                error = RangeError("right eye", EyeRight, EyeMinimum, EyeMaximum);
                return false;
            }

            if (!IsInRange(Mouth, MouthMinimum, MouthMaximum))
            {
                error = RangeError("mouth", Mouth, MouthMinimum, MouthMaximum);
                return false;
            }

            if (!IsInRange(EyebrowLeft, EyebrowMinimum, EyebrowMaximum))
            {
                error = RangeError("left eyebrow", EyebrowLeft, EyebrowMinimum, EyebrowMaximum);
                return false;
            }

            if (!IsInRange(EyebrowRight, EyebrowMinimum, EyebrowMaximum))
            {
                error = RangeError("right eyebrow", EyebrowRight, EyebrowMinimum, EyebrowMaximum);
                return false;
            }

            error = null;
            return true;
        }

        public MiniFaceSelection ClampedClone()
        {
            return new MiniFaceSelection(
                Mathf.Clamp(EyeLeft, EyeMinimum, EyeMaximum),
                Mathf.Clamp(EyeRight, EyeMinimum, EyeMaximum),
                Mathf.Clamp(Mouth, MouthMinimum, MouthMaximum),
                Mathf.Clamp(EyebrowLeft, EyebrowMinimum, EyebrowMaximum),
                Mathf.Clamp(EyebrowRight, EyebrowMinimum, EyebrowMaximum));
        }

        public string ToDisplayString()
        {
            return "Eyes L/R " +
                   EyeLeft.ToString(CultureInfo.InvariantCulture) + "/" +
                   EyeRight.ToString(CultureInfo.InvariantCulture) +
                   ", mouth " + Mouth.ToString(CultureInfo.InvariantCulture) +
                   ", brows L/R " +
                   EyebrowLeft.ToString(CultureInfo.InvariantCulture) + "/" +
                   EyebrowRight.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(MiniFaceSelection other)
        {
            return !ReferenceEquals(other, null) &&
                   EyeLeft == other.EyeLeft &&
                   EyeRight == other.EyeRight &&
                   Mouth == other.Mouth &&
                   EyebrowLeft == other.EyebrowLeft &&
                   EyebrowRight == other.EyebrowRight;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MiniFaceSelection);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + EyeLeft;
                hash = hash * 31 + EyeRight;
                hash = hash * 31 + Mouth;
                hash = hash * 31 + EyebrowLeft;
                hash = hash * 31 + EyebrowRight;
                return hash;
            }
        }

        public override string ToString()
        {
            return ToDisplayString();
        }

        public static bool operator ==(MiniFaceSelection left, MiniFaceSelection right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return !ReferenceEquals(left, null) && left.Equals(right);
        }

        public static bool operator !=(MiniFaceSelection left, MiniFaceSelection right)
        {
            return !(left == right);
        }

        private static bool IsInRange(int value, int minimum, int maximum)
        {
            return value >= minimum && value <= maximum;
        }

        private static string RangeError(string part, int value, int minimum, int maximum)
        {
            return "Mini face " + part + " index " +
                   value.ToString(CultureInfo.InvariantCulture) +
                   " is outside " +
                   minimum.ToString(CultureInfo.InvariantCulture) + ".." +
                   maximum.ToString(CultureInfo.InvariantCulture) + ".";
        }
    }

    internal sealed class MiniFaceMaterials
    {
        private const string EyeRendererName = "M_Eye";
        private const string MouthRendererName = "M_Mouth";
        private const string EyebrowLeftRendererName = "M_Mayu_L";
        private const string EyebrowRightRendererName = "M_Mayu_R";
        private const string EyeUvProperty = "_UVOffset";
        private const float CoordinateTolerance = 0.002f;

        private readonly Material _eye;
        private readonly Material _mouth;
        private readonly Material _eyebrowLeft;
        private readonly Material _eyebrowRight;
        private readonly Vector4 _baselineEyeUv;
        private readonly Vector2 _baselineMouthOffset;
        private readonly Vector2 _baselineEyebrowLeftOffset;
        private readonly Vector2 _baselineEyebrowRightOffset;

        private MiniFaceMaterials(
            Material eye,
            Material mouth,
            Material eyebrowLeft,
            Material eyebrowRight)
        {
            _eye = eye;
            _mouth = mouth;
            _eyebrowLeft = eyebrowLeft;
            _eyebrowRight = eyebrowRight;
            _baselineEyeUv = _eye.GetVector(EyeUvProperty);
            _baselineMouthOffset = _mouth.mainTextureOffset;
            _baselineEyebrowLeftOffset = _eyebrowLeft.mainTextureOffset;
            _baselineEyebrowRightOffset = _eyebrowRight.mainTextureOffset;
        }

        public static bool TryCapture(
            UmaContainerCharacter container,
            out MiniFaceMaterials materials,
            out string error)
        {
            materials = null;
            if (container == null)
            {
                error = "Cannot capture Mini face materials because the character container is missing.";
                return false;
            }

            try
            {
                MeshRenderer[] renderers = container.GetComponentsInChildren<MeshRenderer>(true);
                Material eye;
                Material mouth;
                Material eyebrowLeft;
                Material eyebrowRight;

                if (!TryGetMaterial(renderers, EyeRendererName, out eye, out error) ||
                    !TryGetMaterial(renderers, MouthRendererName, out mouth, out error) ||
                    !TryGetMaterial(renderers, EyebrowLeftRendererName, out eyebrowLeft, out error) ||
                    !TryGetMaterial(renderers, EyebrowRightRendererName, out eyebrowRight, out error))
                {
                    return false;
                }

                if (!eye.HasProperty(EyeUvProperty))
                {
                    error = "Mini face renderer " + EyeRendererName +
                            " does not expose the expected " + EyeUvProperty + " material property.";
                    return false;
                }

                materials = new MiniFaceMaterials(eye, mouth, eyebrowLeft, eyebrowRight);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not capture Mini face materials: " + exception.Message;
                materials = null;
                return false;
            }
        }

        public void RestoreBaseline()
        {
            _eye.SetVector(EyeUvProperty, _baselineEyeUv);
            _mouth.mainTextureOffset = _baselineMouthOffset;
            _eyebrowLeft.mainTextureOffset = _baselineEyebrowLeftOffset;
            _eyebrowRight.mainTextureOffset = _baselineEyebrowRightOffset;
        }

        public bool TryReadCurrent(out MiniFaceSelection selection, out string error)
        {
            selection = null;
            try
            {
                Vector4 eyeUv = _eye.GetVector(EyeUvProperty);
                int eyeLeft;
                int eyeRight;
                int mouth;
                int eyebrowLeft;
                int eyebrowRight;

                if (!TryDecodeIndex(
                        new Vector2(eyeUv.z, eyeUv.w),
                        0.125f,
                        MiniFaceSelection.EyeMaximum,
                        "left eye",
                        out eyeLeft,
                        out error) ||
                    !TryDecodeIndex(
                        new Vector2(eyeUv.x, eyeUv.y),
                        0.125f,
                        MiniFaceSelection.EyeMaximum,
                        "right eye",
                        out eyeRight,
                        out error) ||
                    !TryDecodeIndex(
                        _mouth.mainTextureOffset,
                        0.125f,
                        MiniFaceSelection.MouthMaximum,
                        "mouth",
                        out mouth,
                        out error) ||
                    !TryDecodeIndex(
                        _eyebrowLeft.mainTextureOffset,
                        0.25f,
                        MiniFaceSelection.EyebrowMaximum,
                        "left eyebrow",
                        out eyebrowLeft,
                        out error) ||
                    !TryDecodeIndex(
                        _eyebrowRight.mainTextureOffset,
                        0.25f,
                        MiniFaceSelection.EyebrowMaximum,
                        "right eyebrow",
                        out eyebrowRight,
                        out error))
                {
                    return false;
                }

                selection = new MiniFaceSelection(
                    eyeLeft,
                    eyeRight,
                    mouth,
                    eyebrowLeft,
                    eyebrowRight);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not read the current Mini face: " + exception.Message;
                selection = null;
                return false;
            }
        }

        public bool TryApply(MiniFaceSelection selection, out string error)
        {
            if (selection == null)
            {
                error = "Cannot apply an empty Mini face selection.";
                return false;
            }

            if (!selection.TryValidate(out error))
            {
                return false;
            }

            try
            {
                Vector2 eyeRight = EncodeIndex(selection.EyeRight, 0.125f);
                Vector2 eyeLeft = EncodeIndex(selection.EyeLeft, 0.125f);
                _eye.SetVector(
                    EyeUvProperty,
                    new Vector4(eyeRight.x, eyeRight.y, eyeLeft.x, eyeLeft.y));
                _mouth.mainTextureOffset = EncodeIndex(selection.Mouth, 0.125f);
                _eyebrowLeft.mainTextureOffset = EncodeIndex(selection.EyebrowLeft, 0.25f);
                _eyebrowRight.mainTextureOffset = EncodeIndex(selection.EyebrowRight, 0.25f);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not apply the Mini face selection: " + exception.Message;
                return false;
            }
        }

        private static bool TryGetMaterial(
            MeshRenderer[] renderers,
            string rendererName,
            out Material material,
            out string error)
        {
            material = null;
            MeshRenderer match = null;
            if (renderers != null)
            {
                for (int index = 0; index < renderers.Length; index++)
                {
                    MeshRenderer candidate = renderers[index];
                    if (candidate != null &&
                        candidate.gameObject != null &&
                        string.Equals(candidate.gameObject.name, rendererName, StringComparison.Ordinal))
                    {
                        match = candidate;
                        break;
                    }
                }
            }

            if (match == null)
            {
                error = "Mini face renderer " + rendererName + " was not found; using Auto face is required.";
                return false;
            }

            if (match.sharedMaterial == null)
            {
                error = "Mini face renderer " + rendererName + " has no material; using Auto face is required.";
                return false;
            }

            material = match.material;
            if (material == null)
            {
                error = "Mini face renderer " + rendererName +
                        " could not provide a material; using Auto face is required.";
                return false;
            }

            error = null;
            return true;
        }

        private static Vector2 EncodeIndex(int index, float rowStep)
        {
            return new Vector2(0.25f * (index % 4), -rowStep * (index / 4));
        }

        private static bool TryDecodeIndex(
            Vector2 offset,
            float rowStep,
            int maximum,
            string part,
            out int index,
            out string error)
        {
            int column = Mathf.RoundToInt(offset.x / 0.25f);
            int row = Mathf.RoundToInt(-offset.y / rowStep);
            index = row * 4 + column;

            Vector2 expected = EncodeIndex(index, rowStep);
            bool onGrid = Mathf.Abs(offset.x - expected.x) <= CoordinateTolerance &&
                          Mathf.Abs(offset.y - expected.y) <= CoordinateTolerance;
            if (column < 0 || column > 3 || row < 0 || index < 0 || index > maximum || !onGrid)
            {
                error = "Current Mini face " + part + " UV offset " +
                        FormatOffset(offset) + " does not map to a supported atlas index.";
                index = 0;
                return false;
            }

            error = null;
            return true;
        }

        private static string FormatOffset(Vector2 offset)
        {
            return "(" +
                   offset.x.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
                   offset.y.ToString("0.###", CultureInfo.InvariantCulture) + ")";
        }
    }
}
