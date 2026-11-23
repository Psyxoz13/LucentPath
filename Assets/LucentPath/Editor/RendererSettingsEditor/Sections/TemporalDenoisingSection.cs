using LucentPath.EditorUi.Core;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class TemporalDenoisingSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public TemporalDenoisingSection(LucentPathRendererSettingsEditor editor)
            : base(
                "Temporal Denoising",
                "Controls how history is reused, when it is rejected, and how spatial filtering cleans the result.",
                () => editor.IsTemporalSectionExpanded,
                editor.OnTemporalSectionVisibilityChanged,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new TemporalHistoryCard(editor),
                    new HistoryRejectionCard(editor),
                    new HistoryConfidenceCard(editor),
                    new DisocclusionRecoveryCard(editor),
                    new AtrousFilterCard(editor),
                    new FireflySuppressionCard(editor),
                    new TaaCard(editor)
                })
        { }
    }
}
