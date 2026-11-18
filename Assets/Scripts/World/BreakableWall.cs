using System.Collections;
using UnityEngine;

namespace DrJones
{
    public sealed class BreakableWall : MonoBehaviour
    {
        bool broken;

        public void Break()
        {
            if (broken)
                return;
            broken = true;
            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBreak();
            VisualFactory.Burst(transform.position + Vector3.up * 0.5f, new Color(0.85f, 0.75f, 0.55f, 0.8f), 22);

            var mesh = transform.Find("Mesh");
            if (mesh != null)
                StartCoroutine(Shatter(mesh));
            else
                gameObject.SetActive(false);
        }

        IEnumerator Shatter(Transform mesh)
        {
            var lightGo = new GameObject("BreakFlash");
            lightGo.transform.position = transform.position + Vector3.up * 0.6f;
            var light = lightGo.AddComponent<Light>();
            light.color = new Color(1f, 0.75f, 0.4f);
            light.range = 4f;
            light.intensity = 8f;
            for (int i = 0; i < 7; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.transform.position = mesh.position + Random.insideUnitSphere * 0.2f;
                chunk.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
                chunk.GetComponent<Renderer>().sharedMaterial = mesh.GetComponent<Renderer>().sharedMaterial;
                var col = chunk.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
                chunk.AddComponent<DebrisChunk>().Kick(Random.insideUnitSphere * 2.4f + Vector3.up * 2f);
            }

            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(8f, 0f, t / 0.25f);
                mesh.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t / 0.25f);
                yield return null;
            }

            Destroy(lightGo);
            mesh.gameObject.SetActive(false);
        }
    }

    public sealed class DebrisChunk : MonoBehaviour
    {
        Vector3 velocity;

        public void Kick(Vector3 initial)
        {
            velocity = initial;
            Destroy(gameObject, 1.3f);
        }

        void Update()
        {
            velocity += Vector3.down * 12f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(velocity * 40f * Time.deltaTime, Space.World);
        }
    }
}
