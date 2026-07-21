using UnityEngine;
using UnityEngine.UI;

namespace FinalDefense.UI
{
    [AddComponentMenu("UI/Effects/Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public enum GradientDirection
        {
            Vertical,
            Horizontal,
            Diagonal,
        }

        public Color topColor = Color.white;
        public Color bottomColor = Color.white;
        public GradientDirection gradientDirection = GradientDirection.Vertical;

        public Color TopColor
        {
            get => topColor;
            set { topColor = value; if (graphic != null) graphic.SetVerticesDirty(); }
        }

        public Color BottomColor
        {
            get => bottomColor;
            set { bottomColor = value; if (graphic != null) graphic.SetVerticesDirty(); }
        }

        public GradientDirection Direction
        {
            get => gradientDirection;
            set { gradientDirection = value; if (graphic != null) graphic.SetVerticesDirty(); }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var vertex = new UIVertex();
            int count = vh.currentVertCount;

            if (count == 0)
                return;

            var bottomLeft = Vector2.zero;
            var topRight = Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector2 pos = vertex.position;

                if (i == 0)
                {
                    bottomLeft = pos;
                    topRight = pos;
                }
                else
                {
                    bottomLeft.x = Mathf.Min(bottomLeft.x, pos.x);
                    bottomLeft.y = Mathf.Min(bottomLeft.y, pos.y);
                    topRight.x = Mathf.Max(topRight.x, pos.x);
                    topRight.y = Mathf.Max(topRight.y, pos.y);
                }
            }

            float width = topRight.x - bottomLeft.x;
            float height = topRight.y - bottomLeft.y;

            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector2 pos = vertex.position;

                float t = 0;
                switch (gradientDirection)
                {
                    case GradientDirection.Vertical:
                        t = (pos.y - bottomLeft.y) / height;
                        break;
                    case GradientDirection.Horizontal:
                        t = (pos.x - bottomLeft.x) / width;
                        break;
                    case GradientDirection.Diagonal:
                        float tx = (pos.x - bottomLeft.x) / width;
                        float ty = (pos.y - bottomLeft.y) / height;
                        t = (tx + ty) * 0.5f;
                        break;
                }

                vertex.color = Color.Lerp(bottomColor, topColor, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
