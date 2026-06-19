using System.Collections;

namespace ViewSystem
{
    /// <summary>
    /// after view has been added, and initially focused, replacement of Start()
    /// </summary>
    public interface IViewInitialFocusListener
    {
        void OnViewInitialFocus();
    }
    
    /// <summary>
    /// after view has been added, and initially focused, replacement of Start()
    /// </summary>
    public interface IViewOpenWithoutActionListener
    {
        void OnViewOpenWithoutAction();
    }

    /// <summary>
    /// after view has been added
    /// </summary>
    public interface IViewAddedListener
    {
        void OnViewAdded();
    }

    /// <summary>
    /// before removal of a view
    /// </summary>
    public interface IViewRemovedListener
    {
        void OnViewRemoved();
    }

    /// <summary>
    /// after focus state changed, called after InitialFocus
    /// </summary>
    public interface IViewFocusListener
    {
        void OnViewFocus(bool focusStatus);
    }

    /// <summary>
    /// when back button is detected and view is currently focused
    /// automatically pop view if not implemented
    /// </summary>
    public interface IViewBackButtonListener
    {
        void OnViewBackButton();
    }

    /// <summary>
    /// update function only when view is visible
    /// </summary>
    public interface IViewUpdateListener
    {
        void OnViewUpdate();
    }

    /// <summary>
    /// after visiblity state changed 
    /// </summary>
    public interface IViewVisibleListener
    {
        void OnViewVisible(bool visibleState);
    }
    public interface IViewRoot { }

    public interface ITopbarBackButtonListener
    {
        IEnumerator OnCoTopbarBackButton();
    }
}