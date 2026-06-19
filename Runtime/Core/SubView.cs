using System.Collections.Generic;
using UnityEngine;
using ViewSystem;

public class SubView : MonoBehaviour
{
    [System.Serializable]
    public class Reference<TSubview, THolder> where TSubview : SubView,
        IServiceLocatorComponent where THolder : ViewBase, ISubViewHolder
    {
        public TSubview RetainedView { get; private set; }
        public Transform ParentTransform;

        public void Retain()
        {
            // var subView = ViewManager.Get<THolder>().GetSubView<TSubview>();
            var subView = ViewManager.Get<THolder>().GetServiceLocator().Resolve<TSubview>();
            subView.RetainSubView(ParentTransform);
            RetainedView = subView;
        }

        public void Release()
        {
            RetainedView.ReleaseSubView(ParentTransform);
            RetainedView = null;
        }
    }

    readonly List<Transform> m_TransformDepth = new();

    protected virtual void Awake()
    {
        m_TransformDepth.Add(transform.parent);
        OnSetNewParent(transform.parent);
    }

    private void RetainSubView(Transform parent)
    {
        m_TransformDepth.Add(parent);
        transform.SetParent(parent, false);
        OnSetNewParent(parent);
    }

    protected virtual void OnSetNewParent(Transform parent) { }

    private void ReleaseSubView(Transform parent)
    {
        if (!m_TransformDepth.Remove(parent)) throw new System.Exception("Releasing Transform has never been retained!");

        if (parent != transform.parent) return;

        if (m_TransformDepth.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        var newParent = m_TransformDepth[^1];
        transform.SetParent(newParent);
        OnSetNewParent(parent);
    }
}

public interface ISubViewHolder
{
    public IServiceLocator GetServiceLocator();
}