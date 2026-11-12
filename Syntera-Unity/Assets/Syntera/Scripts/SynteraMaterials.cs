using UnityEngine;

namespace Syntera
{
    // Builds strip shaders that nothing references, so Shader.Find("Standard") can return null in a
    // player even though it works in the editor. The build includes Standard explicitly
    // (SynteraEditorTools); this helper also falls back to the built-in default material's shader.
    public static class SynteraMaterials
    {
        private static Shader shader;

        public static Material Make(Color color)
        {
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                if (shader == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shader = probe.GetComponent<Renderer>().sharedMaterial.shader;
                    Object.Destroy(probe);
                }
            }
            return new Material(shader) { color = color };
        }
    }
}
