using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace MalbersAnimations
{
    [AddComponentMenu("Malbers/UI/Fade In-Out Graphic")]

    public class FadeInOutGraphic : MonoBehaviour
    {
        public CanvasGroup group;
        public float time = 0.25f;
        public AnimationCurve fadeCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, 0), new Keyframe(1, 1) });

        private void Reset()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) gameObject.AddComponent<CanvasGroup>();
        }

        public virtual void Fade_In_Out(bool fade)
        {
            if (fade) Fade_In();

            else Fade_Out();
        }

        public virtual void Fade_In()
        {
            StopAllCoroutines();
            StartCoroutine(C_Fade(1));
        }

        public virtual void Fade_Out()
        {
            StopAllCoroutines();
            StartCoroutine(C_Fade(0));
        }

        private IEnumerator C_Fade(float value)
        {
            float elapsedTime = 0;
            float startAlpha = group.alpha;

            while ((time > 0) && (elapsedTime <= time))
            {
                float result = fadeCurve != null ? fadeCurve.Evaluate(elapsedTime / time) : elapsedTime / time;               //Evaluation of the Pos curve

                group.alpha = Mathf.Lerp(startAlpha, value, result);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            group.alpha = value;
            yield return null;
        }
    }
}
