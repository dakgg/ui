using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ViewSystem;

[DisallowMultipleComponent, ExecuteAlways]
public class UIAnimation : MonoBehaviour, IViewVisibleListener
{
    public AnimationClip AnimationToPlay;
    public bool AutoPlay = true;
    public bool AutoPlayOnlyVisible = false;
    public float DelayOnFirstFrame = 0f;
    public float LoopOffsetRatio = 0f;

    public List<AnimationClip> EditingClips;
    float m_SampleBeforeStart = 0f;
    AnimationClip m_ClipBeforeStart = null;

    bool m_IsRealtime = true;
    bool m_Started = false;
    int m_Version;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        m_Started = true;
        if (!Application.isPlaying) return;

        if(m_ClipBeforeStart != null) SampleProgress(m_ClipBeforeStart, m_SampleBeforeStart);
        
        if (AutoPlay)
        {
            if(AutoPlayOnlyVisible) ViewSystem.UIVisiblity.RegisterVisible(this, gameObject);
            else ShowAnimation();
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
        {
            var animComponent = GetComponent<Animation>();
            if (animComponent == null) animComponent = gameObject.AddComponent<Animation>();
            animComponent.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            UnityEditor.AnimationUtility.SetAnimationClips(animComponent, CollectClips());
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            var animComponent = GetComponent<Animation>();
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (animComponent != null) DestroyImmediate(animComponent);
            };
        }
    }
#endif

    private void OnEnable()
    {
        if (!Application.isPlaying || !m_Started) return;
        if (m_Started) SampleProgress(0);
        if (AutoPlay && !AutoPlayOnlyVisible) ShowAnimation();
    }

    public void SampleProgress(float progress)
    {
        SampleProgress(AnimationToPlay, progress);
    }

    public void SampleProgress(AnimationClip clip, float progress)
    {
        if(clip == null) return;

        if(m_Started)
        {
            if (!clip.legacy)
            {
                Debug.LogWarning($"{clip.name} is non-legacy clip, not for ui animation");
            }
            
            clip.SampleAnimation(gameObject, Mathf.Lerp(progress, 0, clip.length));
        }
        else
        {
            m_ClipBeforeStart = clip;
            m_SampleBeforeStart = progress;
        }
    }

    public Coroutine ShowAnimation(bool isRealtime = true)
    {
        return StartCoroutine(CoShowAnimation(AnimationToPlay, ++m_Version, isRealtime));
    }

    /// <summary>
    /// animationWith custom clip
    /// </summary>
    public Coroutine ShowAnimation(AnimationClip clip, bool isRealtime = true)
    {
        return StartCoroutine(CoShowAnimation(clip, ++m_Version, isRealtime));
    }

    IEnumerator CoShowAnimation(AnimationClip clip, int version, bool isRealtime)
    {
        if (clip == null) yield break;

        if (!clip.legacy)
        {
            Debug.LogWarning($"{clip.name} is non-legacy clip, not for ui animation");
        }

        clip.SampleAnimation(gameObject, 0);
        var events = clip.events;
        yield return new WaitForSecondsRealtime(DelayOnFirstFrame);
        if(version != m_Version) yield break;
        
        var startTime = isRealtime ? Time.realtimeSinceStartup : 0;

        switch(clip.wrapMode)
        {
            case WrapMode.Loop:
                while (true)
                {
                    var sampleTime = isRealtime ? Time.realtimeSinceStartup - startTime : startTime += Time.deltaTime;
                    if (sampleTime > clip.length)
                    {
                        var timeRemainder = sampleTime - clip.length;
                        var offsetTime = LoopOffsetRatio * clip.length;
                        clip.SampleAnimation(gameObject, offsetTime + timeRemainder % (clip.length - offsetTime));
                    }
                    else
                    {
                        clip.SampleAnimation(gameObject, sampleTime);
                    }

                    yield return null;
                    if(version != m_Version) yield break;
                }
            case WrapMode.PingPong:
                while (true)
                {
                    clip.SampleAnimation(gameObject, isRealtime ? Time.realtimeSinceStartup - startTime : startTime += Time.deltaTime);
                    yield return null;
                    if(version != m_Version) yield break;
                }
            default:

                int clipEventIndex = 0; 
                for (var f = 0f; f < clip.length; f += isRealtime ? Time.unscaledDeltaTime : Time.deltaTime)
                {
                    var clipTime = isRealtime ? Time.realtimeSinceStartup - startTime : startTime += Time.deltaTime;
                    clip.SampleAnimation(gameObject, clipTime);

                    if (events.Length > 0 && events.Length > clipEventIndex)
                    {
                        var currentEvent = events[clipEventIndex];
                        if (currentEvent.time <= clipTime)
                        {
                            if (string.IsNullOrEmpty(currentEvent.functionName))
                            {
                                Debug.LogError($"{clip.name} has empty Event");
                                clipEventIndex++;
                                continue;
                            }

                            CallEvent(currentEvent);
                            clipEventIndex++;
                        }
                    }

                    yield return null;
                    if(version != m_Version) yield break;
                }
                clip.SampleAnimation(gameObject, clip.length);
                break;
        }
    }

    private static List<MonoBehaviour> s_EventListCache = new(5);
    private static object[] s_InvokeParameter = new object[1];
    
    void CallEvent(AnimationEvent animEvent)
    {
        gameObject.GetComponents(s_EventListCache);
        for(int i = 0; i < s_EventListCache.Count; i++)
        {
            var current = s_EventListCache[i];
            var method = current.GetType().GetMethod(animEvent.functionName, BindingFlags.Instance | BindingFlags.Public);
            if(method == null) continue;
            var parameters = method.GetParameters();
            if(parameters.Length > 1) continue;
            
            if (parameters.Length == 0)
            {
                method.Invoke(current, null);
                break;
            }
            
            var firstParam = parameters[0];
            if (firstParam.ParameterType == typeof(string))
            {
                s_InvokeParameter[0] = animEvent.stringParameter;
                method.Invoke(current, s_InvokeParameter);
                break;
            }
            
            if(firstParam.ParameterType == typeof(int))
            {
                s_InvokeParameter[0] = animEvent.intParameter;
                method.Invoke(current, s_InvokeParameter);
                break;
            }
            
            if(firstParam.ParameterType == typeof(float))
            {
                s_InvokeParameter[0] = animEvent.floatParameter;
                method.Invoke(current, s_InvokeParameter);
                break;
            }
            
            if(typeof(Object).IsAssignableFrom(firstParam.ParameterType))
            {
                s_InvokeParameter[0] = animEvent.objectReferenceParameter;
                method.Invoke(current, s_InvokeParameter);
                break;
            }
        }
        
        s_EventListCache.Clear();
    }

    void IViewVisibleListener.OnViewVisible(bool visibleState)
    {
        if (visibleState) ShowAnimation();
        else
        {
            m_Version++;
        }
    }

    AnimationClip[] CollectClips()
    {
        var clips = new List<AnimationClip>();
        if (AnimationToPlay != null) clips.Add(AnimationToPlay);
        if (EditingClips != null)
        {
            foreach (var clip in EditingClips)
            {
                if(clip == null) continue;
                clips.Add(clip);
            }
        }

        return clips.ToArray();
    }
}
