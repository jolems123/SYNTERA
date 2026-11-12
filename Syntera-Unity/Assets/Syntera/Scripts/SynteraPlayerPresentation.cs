using UnityEngine;

namespace Syntera
{
    // Lightweight animated player fallback while licensed Humanoid character assets
    // are unavailable. Not a replacement for skinned mesh animation or mocap.
    public sealed class SynteraPlayerPresentation : MonoBehaviour
    {
        private Transform leftArm, rightArm, leftLeg, rightLeg, head;
        private Vector3 last;
        private float gait;
        private bool usingHumanoid;

        private void Start()
        {
            usingHumanoid = GetComponent<SynteraHumanoidMotionBridge>() != null;
            last = transform.position;
            if (usingHumanoid) { enabled = false; return; }
            leftArm = Part("Player arm L", new Vector3(-.53f,.1f,0f),new Vector3(.22f,.72f,.28f));
            rightArm = Part("Player arm R",new Vector3(.53f,.1f,0f),new Vector3(.22f,.72f,.28f));
            leftLeg = Part("Player leg L",new Vector3(-.23f,-.77f,0f),new Vector3(.32f,.77f,.32f));
            rightLeg = Part("Player leg R",new Vector3(.23f,-.77f,0f),new Vector3(.32f,.77f,.32f));
            head = Part("Player head",new Vector3(0f,.83f,0f),new Vector3(.65f,.52f,.57f));
        }

        private Transform Part(string name, Vector3 position, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            var col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var renderer = part.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(.2f,.6f,.76f);
            return part.transform;
        }

        private void Update()
        {
            float speed = Vector3.Distance(last, transform.position) / Mathf.Max(Time.deltaTime,.015f);
            last = transform.position;
            float moving = Mathf.Clamp01(speed / 4f);
            gait += Time.deltaTime * Mathf.Lerp(2f,8f,moving);
            float swing = Mathf.Sin(gait) * moving * 27f;
            leftArm.localRotation = Quaternion.Euler(-swing,0,0);
            rightArm.localRotation = Quaternion.Euler(swing,0,0);
            leftLeg.localRotation = Quaternion.Euler(swing,0,0);
            rightLeg.localRotation = Quaternion.Euler(-swing,0,0);
            head.localRotation = Quaternion.Euler(0,Mathf.Sin(Time.time*.6f)*2f,0);
        }
    }
}
