using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.UI.Builder
{
    internal class BuilderExplorerDragger : BuilderDragger
    {
        protected struct ElementToReparent
        {
            public VisualElement element;
            public VisualElement oldParent;
            public int oldIndex;
        }

        static protected readonly string s_DraggableStyleClassPillClassName = "unity-builder-class-pill--draggable";

        protected VisualElement m_DragPreviewLastParent;
        protected int m_DragPreviewLastIndex;

        protected VisualElement m_TargetElementToReparent;

        protected List<ElementToReparent> m_ElementsToReparent = new List<ElementToReparent>();

        public BuilderExplorerDragger(
            BuilderPaneWindow paneWindow,
            VisualElement root, BuilderSelection selection,
            BuilderViewport viewport = null, BuilderParentTracker parentTracker = null)
            : base(paneWindow, root, selection, viewport, parentTracker)
        {

        }

        protected virtual bool ExplorerCanStartDrag(VisualElement targetElement)
        {
            return true;
        }

        protected virtual string ExplorerGetDraggedPillText(VisualElement targetElement)
        {
            return targetElement.name;
        }

        protected virtual void ExplorerPerformDrag()
        {

        }

        protected virtual void ResetDragPreviewElement()
        {
            if (m_DragPreviewLastParent == null)
                return;

            m_DragPreviewLastParent = null;
        }

        protected override VisualElement CreateDraggedElement()
        {
            var classPillTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                BuilderConstants.UIBuilderPackagePath + "/BuilderClassPill.uxml");
            var pill = classPillTemplate.CloneTree();
            pill.AddToClassList(s_DraggableStyleClassPillClassName);
            return pill;
        }

        protected override bool StartDrag(VisualElement target, Vector2 mousePosition, VisualElement pill)
        {
            m_ElementsToReparent.Clear();

            // Create list of elements to reparent.
            foreach (var selectedElement in selection.selection)
            {
                if (!selectedElement.IsPartOfCurrentDocument())
                    continue;

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
            pillLabel.text = ExplorerGetDraggedPillText(m_TargetElementToReparent);

            // Remove the yellow text color from pill if dragger element does not represent a VisualElementAsset.
            if (m_TargetElementToReparent.GetVisualElementAsset() != null)
                pillLabel.RemoveFromClassList(BuilderConstants.ElementClassNameClassName);

            return true;
        }

        protected override void PerformDrag(VisualElement target, VisualElement pickedElement, int index = -1)
        {
            if (pickedElement == null)
            {
                FailAction(target);
                return;
            }

            if (pickedElement == m_DragPreviewLastParent && index == m_DragPreviewLastIndex)
                return;

            ResetDragPreviewElement();

            m_DragPreviewLastParent = pickedElement;
            m_DragPreviewLastIndex = index;

            m_DragPreviewLastParent.HideMinSizeSpecialElement();

            FixElementSizeAndPosition(m_DragPreviewLastParent);

            ExplorerPerformDrag();

            Reparent(pickedElement, index);
        }

        void Reparent(VisualElement newParent, int index)
        {
            foreach (var elementToReparent in m_ElementsToReparent)
                index = ReparentIndividualElement(elementToReparent.element, newParent, index);
        }

        int ReparentIndividualElement(VisualElement element, VisualElement newParent, int index)
        {
            var elementToReparent = element;

            if (newParent == elementToReparent)
                return index;

            var oldParent = elementToReparent.parent;
            if (oldParent != newParent)
            {
                if (index < 0 || index > newParent.childCount - 1)
                    newParent.Insert(newParent.childCount, elementToReparent);
                else
                    newParent.Insert(index, elementToReparent);

                if (index < newParent.childCount)
                    return index + 1;
                else
                    return index;
            }

            if (index < 0)
                index = newParent.childCount;

            var oldIndex = oldParent.IndexOf(elementToReparent);
            if (oldIndex < index)
                index = index - 1;

            elementToReparent.RemoveFromHierarchy();
            newParent.Insert(index, elementToReparent);

            return index + 1;
        }

        protected override void EndDrag()
        {
            ResetDragPreviewElement();
        }

        protected override void FailAction(VisualElement target)
        {
            if (m_DragPreviewLastParent == null)
                return;

            ResetDragPreviewElement();

            foreach (var elementToReparent in m_ElementsToReparent)
                ReparentIndividualElement(
                    elementToReparent.element,
                    elementToReparent.oldParent,
                    elementToReparent.oldIndex);
        }
    }
}
