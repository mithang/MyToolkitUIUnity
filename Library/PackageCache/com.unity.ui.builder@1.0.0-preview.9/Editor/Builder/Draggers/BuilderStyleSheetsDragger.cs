using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.UI.Builder
{
    internal class BuilderStyleSheetsDragger : BuilderExplorerDragger
    {
        public BuilderStyleSheetsDragger(
            BuilderPaneWindow paneWindow,
            VisualElement root, BuilderSelection selection)
            : base(paneWindow, root, selection)
        {

        }

        protected override bool StartDrag(VisualElement target, Vector2 mousePosition, VisualElement pill)
        {
            m_ElementsToReparent.Clear();

            // Create list of elements to reparent.
            foreach (var selectedElement in selection.selection)
            {
                var elementToReparent = new ElementToReparent()
                {
                    element = selectedElement,
                    oldParent = selectedElement.parent,
                    oldIndex = selectedElement.parent.IndexOf(selectedElement)
                };

                m_ElementsToReparent.Add(elementToReparent);
            }

            // We still need a primary element that is "being dragged" for visualization purporses.
            m_TargetElementToReparent = target.GetProperty(BuilderConstants.ExplorerItemElementLinkVEPropertyName) as VisualElement;
            if (!ExplorerCanStartDrag(m_TargetElementToReparent))
                return false;

            // We use the primary target element for our pill info.
            var pillLabel = pill.Q<Label>();
            pillLabel.text = m_TargetElementToReparent.IsSelector()
                ? StyleSheetToUss.ToUssSelector(m_TargetElementToReparent.GetStyleComplexSelector())
                : m_TargetElementToReparent.GetStyleSheet().name + BuilderConstants.UssExtension;

            pillLabel.RemoveFromClassList(BuilderConstants.ElementClassNameClassName);

            return true;
        }

        protected override bool ExplorerCanStartDrag(VisualElement targetElement)
        {
            bool readyForDrag = (targetElement.IsSelector() || targetElement.IsStyleSheet()) && !targetElement.IsParentSelector();
            return readyForDrag;
        }

        protected override string ExplorerGetDraggedPillText(VisualElement targetElement)
        {
            return targetElement.IsSelector()
                ? StyleSheetToUss.ToUssSelector(targetElement.GetStyleComplexSelector())
                : targetElement.GetStyleSheet().name + BuilderConstants.UssExtension;
        }

        protected override void PerformAction(VisualElement destination, DestinationPane pane, int index = -1)
        {
            if (m_TargetElementToReparent.IsSelector())
                PerformActionForSelector(destination, pane, index);
            else if (m_TargetElementToReparent.IsStyleSheet())
                PerformActionForStyleSheet(destination, pane, index);
        }

        void PerformActionForSelector(VisualElement destination, DestinationPane pane, int index = -1)
        {
            var newStyleSheetElement = destination;

            bool undo = true;

            foreach (var elementToReparent in m_ElementsToReparent)
            {
                var selectorElementToReparent = elementToReparent.element;
                var oldStyleSheetElement = elementToReparent.oldParent;

                if (newStyleSheetElement == oldStyleSheetElement)
                    continue;

                BuilderSharedStyles.MoveSelectorBetweenStyleSheets(
                    oldStyleSheetElement, newStyleSheetElement, selectorElementToReparent, undo);

                undo = false;
            }

            BuilderSharedStyles.MatchSelectorElementOrderInAsset(newStyleSheetElement, undo);

            selection.NotifyOfHierarchyChange();
            selection.NotifyOfStylingChange(null);
            selection.ForceReselection();
        }

        void PerformActionForStyleSheet(VisualElement destination, DestinationPane pane, int index = -1)
        {
            if (destination == null)
                destination = BuilderSharedStyles.GetSelectorContainerElement(selection.documentRootElement);

            BuilderAssetUtilities.ReorderStyleSheetsInAsset(paneWindow.document, destination);

            selection.NotifyOfHierarchyChange();
            selection.NotifyOfStylingChange(null);
            selection.ForceReselection();
        }

        protected override bool IsPickedElementValid(VisualElement element)
        {
            if (element == null)
                return true;

            var newParent = element;
            foreach (var elementToReparent in m_ElementsToReparent)
                if (newParent == elementToReparent.element)
                    return false;

            if (!element.IsStyleSheet()) // Can only parent selectors under a StyleSheet.
                return false;

            // Check if USS is part of active document.
            if (!string.IsNullOrEmpty(element.GetProperty(BuilderConstants.ExplorerItemLinkedUXMLFileName) as string))
                return false;

            return true;
        }

        protected override bool SupportsDragBetweenElements(VisualElement element)
        {
            if (element == null)
                return false;

            var newParent = element;
            foreach (var elementToReparent in m_ElementsToReparent)
            {
                var toReparent = elementToReparent.element;

                if (newParent == toReparent)
                    return false;

                if (element.IsParentSelector() || toReparent.IsParentSelector())
                    return false;

                if (element.IsSelector() && !toReparent.IsSelector())
                    return false;

                if (element.IsStyleSheet() && toReparent.IsSelector())
                    return false;

                // Check if USS is part of active document.
                if (element.IsStyleSheet() && toReparent.IsStyleSheet() && !string.IsNullOrEmpty(element.GetProperty(BuilderConstants.ExplorerItemLinkedUXMLFileName) as string))
                    return false;
            }

            return true;
        }

        protected override bool SupportsDragInEmptySpace()
        {
            if (paneWindow.document.activeOpenUXMLFile.openUSSFiles.Count != BuilderSharedStyles.GetSelectorContainerElement(selection.documentRootElement).childCount)
                return false;

            return true;
        }

        protected override VisualElement GetDefaultTargetElement()
        {
            if (m_TargetElementToReparent.IsSelector())
                return BuilderSharedStyles.GetSelectorContainerElement(paneWindow.rootVisualElement).Children().Last();

            if (m_TargetElementToReparent.IsStyleSheet())
                return BuilderSharedStyles.GetSelectorContainerElement(paneWindow.rootVisualElement);

            return null;
        }
    }
}