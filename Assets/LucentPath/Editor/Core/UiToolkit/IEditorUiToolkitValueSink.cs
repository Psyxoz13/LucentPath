using System;

namespace LucentPath.EditorUi.Core
{
    public interface IEditorUiToolkitValueSink
    {
        void CommitUiToolkitValueChange<TValue>(string cardTitle, string label, TValue newValue, Action<TValue> assign);
    }
}
