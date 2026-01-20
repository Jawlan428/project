using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace XRMultiplayer
{
    public class VideoPlayerTutorial : MonoBehaviour
    {
        [SerializeField] GameObject m_InfoButtonObject;
        [SerializeField] GameObject m_VideoPlayerObject;
        [SerializeField] TMP_Dropdown m_Dropdown;
        [SerializeField] TMP_Text m_HeaderText;
        [SerializeField] VideoClip[] m_VideoClips;
        [SerializeField] float m_HideDistance = 10.0f;
        [SerializeField] bool m_AutoDisplay = false;
        [SerializeField] CanvasGroup m_InfoButtonFadeGroup;
        [SerializeField] CanvasGroup m_VideoFadeGroup;
        [SerializeField] RawImage m_VideoImage;
        [SerializeField] VideoPlayer m_VideoPlayer;
        [SerializeField] float m_FadeSpeedVideo = 5.0f;
        [SerializeField] float m_FadeSpeedButton = 5.0f;

        Camera m_MainCam;

        bool m_IsHidden = false;
        bool m_IsValid = false;

        IEnumerator m_FadeEnumerator;

        void Start()
        {
            m_MainCam = Camera.main;

            // Validate required references
            if (m_VideoFadeGroup == null || m_InfoButtonFadeGroup == null || 
                m_InfoButtonObject == null || m_VideoPlayerObject == null)
            {
                Debug.LogWarning("[VideoPlayerTutorial] Required references not assigned, disabling tutorial.");
                m_IsValid = false;
                enabled = false;
                return;
            }
            m_IsValid = true;

            if (m_Dropdown != null)
            {
                if (m_VideoClips == null || m_VideoClips.Length < 2)
                {
                    m_Dropdown.gameObject.SetActive(false);
                }
                else
                {
                    // Clear the dropdown options
                    m_Dropdown.ClearOptions();
                    foreach (var c in m_VideoClips)
                    {
                        TMP_Dropdown.OptionData optionData = new TMP_Dropdown.OptionData(c.name);
                        m_Dropdown.options.Add(optionData);
                    }
                    m_Dropdown.onValueChanged.AddListener(PickVideo);
                }
            }

            Hide();
        }

        void PickVideo(int index)
        {
            m_VideoPlayer.Stop();
            m_VideoPlayer.clip = m_VideoClips[index];
            m_HeaderText.text = m_VideoClips[index].name;
            m_VideoPlayer.targetTexture.Release();
            m_VideoPlayer.Play();
        }

        void Update()
        {
            if (!m_IsValid || m_MainCam == null) return;

            if (Vector3.Distance(m_MainCam.transform.position, transform.position) < m_HideDistance)
            {
                if (m_IsHidden)
                {
                    EnableTutorial();
                }
            }
            else
            {
                if (!m_IsHidden)
                {
                    HideTutorial();
                }
            }
        }

        void HideTutorial()
        {
            if (!m_IsValid) return;
            m_IsHidden = true;
            if (m_FadeEnumerator != null) StopCoroutine(m_FadeEnumerator);
            m_FadeEnumerator = FadeOutRoutine();
            StartCoroutine(m_FadeEnumerator);
        }

        void EnableTutorial()
        {
            if (!m_IsValid) return;
            m_IsHidden = false;
            ToggleVideo(m_AutoDisplay);
        }

        /// <summary>
        /// Called from UI button press.
        /// </summary>
        /// <param name="toggle"></param>
        public void ToggleVideo(bool toggle)
        {
            if (!m_IsValid) return;

            m_InfoButtonObject.SetActive(!toggle);
            m_VideoPlayerObject.SetActive(toggle);

            if (toggle)
            {
                m_VideoFadeGroup.alpha = 0.0f;
                if (m_VideoImage != null && m_VideoImage.material != null)
                    m_VideoImage.material.color = new Color(1, 1, 1, m_VideoFadeGroup.alpha);
                if (m_VideoPlayer != null && m_VideoPlayer.targetTexture != null)
                    m_VideoPlayer.targetTexture.Release();
                if (m_FadeEnumerator != null) StopCoroutine(m_FadeEnumerator);
                m_FadeEnumerator = FadeVideoRoutine();
                StartCoroutine(m_FadeEnumerator);
            }
            else
            {
                m_InfoButtonFadeGroup.alpha = 0.0f;
                if (m_FadeEnumerator != null) StopCoroutine(m_FadeEnumerator);
                m_FadeEnumerator = FadeButtonRoutine();
                StartCoroutine(m_FadeEnumerator);
            }
        }

        IEnumerator FadeButtonRoutine()
        {
            while (m_InfoButtonFadeGroup.alpha < 1.0f)
            {
                m_InfoButtonFadeGroup.alpha += Time.deltaTime * m_FadeSpeedButton;
                yield return null;
            }
        }

        IEnumerator FadeVideoRoutine()
        {
            while (m_VideoFadeGroup != null && m_VideoFadeGroup.alpha < 1.0f)
            {
                m_VideoFadeGroup.alpha += Time.deltaTime * m_FadeSpeedVideo;
                if (m_VideoImage != null && m_VideoImage.material != null)
                    m_VideoImage.material.color = new Color(1, 1, 1, m_VideoFadeGroup.alpha);
                yield return null;
            }
        }

        IEnumerator FadeOutRoutine()
        {
            if (m_VideoFadeGroup == null || m_InfoButtonFadeGroup == null)
            {
                yield break;
            }

            while (m_VideoFadeGroup.alpha > 0.0f || m_InfoButtonFadeGroup.alpha > 0.0f)
            {
                float fadeAmount = Time.deltaTime * m_FadeSpeedVideo;
                m_VideoFadeGroup.alpha -= fadeAmount;
                if (m_VideoImage != null && m_VideoImage.material != null)
                    m_VideoImage.material.color = new Color(1, 1, 1, m_VideoFadeGroup.alpha);

                m_InfoButtonFadeGroup.alpha -= fadeAmount;
                yield return null;
            }

            Hide();
            if (m_VideoPlayer != null && m_VideoPlayer.targetTexture != null)
                m_VideoPlayer.targetTexture.Release();
        }

        void Hide()
        {
            if (m_InfoButtonObject != null) m_InfoButtonObject.SetActive(false);
            if (m_VideoPlayerObject != null) m_VideoPlayerObject.SetActive(false);
            m_IsHidden = true;
        }
    }
}
