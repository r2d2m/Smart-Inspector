
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AV.Inspector.Runtime;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AV.Inspector
{
    internal class EditorDraggingPatch : PatchBase
    {
        const int LongHoldMS = 600;
        const int SlowMouseMoveDelayMS = 400;
        const float SlowMouseDeltaThreshold = 40;
        
        const float DragAreaHeight = 22;
        const float DragMarkerHeight = 4;
        const float DragMarkerY = 2;
        
        static readonly Type EditorDraggingType = EditorAssembly.GetType("UnityEditor.EditorDragging");
    
        static Action onPerformDrag;
        static bool IsAnyDragging;
        
        static Vector2 mousePos;
        static Vector2 holdMousePos;
        
        
        [InitializeOnInspector]
        static void OnInspector()
        {
            Runtime.SmartInspector.OnSetupEditorElement += OnSetupEditorElement;
        }

        protected override IEnumerable<Patch> GetPatches()
        {
            yield return new Patch(AccessTools.Method(EditorDraggingType, "GetTargetRect"), postfix: nameof(GetTargetRect_));
            yield return new Patch(AccessTools.Method(EditorDraggingType, "GetMarkerRect"), postfix: nameof(GetMarkerRect_));
            yield return new Patch(AccessTools.Method(EditorDraggingType, "HandleDragPerformEvent"), prefix: nameof(_HandleDragPerformEvent));
            
            yield return new Patch(AccessTools.Method(EditorElementRef.type, "UpdateInspectorVisibility"), prefix: nameof(_UpdateInspectorVisibility));
        }
        
        static void OnSetupEditorElement(Runtime.SmartInspector.EditorElement x)
        {
            var element = x.element.x;
            var footer = x.footer.x as IMGUIContainer;
            var window = x.window;
            
            var dragging = false;
            
            // Footer == Drag/Drop Area
            footer.visible = false;
            footer.pickingMode = PickingMode.Ignore;
            footer.style.position = Position.Absolute;
            footer.style.left = 0;
            footer.style.right = 0;
            footer.style.bottom = 0;
            footer.style.height = DragAreaHeight;
            footer.onGUIHandler += () =>
            {
                if (!IsAnyDragging)
                    return;
                
                if (dragging)
                    mousePos = Event.current.mousePosition;
            };
            
            element.RegisterCallback<DragEnterEvent>(evt => EnterDrag());
            
            element.RegisterCallback<MouseLeaveEvent>(evt => LeaveDrag());
            element.RegisterCallback<DragLeaveEvent>(evt => LeaveDrag());
            element.RegisterCallback<DragPerformEvent>(evt => LeaveDrag());

            // Hides drag area when user is holding mouse still to drag into object field or expand header.
            async void LeaveOnLongHold()
            {
                // Don't leave until mouse is at rest! Ignore small delta
                while ((holdMousePos - mousePos).magnitude > SlowMouseDeltaThreshold)
                {
                    holdMousePos = mousePos;
                    
                    await Task.Delay(SlowMouseMoveDelayMS);
                    
                    window.Repaint();
                }

                await Task.Delay(LongHoldMS);

                if (dragging)
                    LeaveDrag();

                window.Repaint();
            }


            void OnBeginDrag()
            {
            }
            void OnPerformDrag()
            {
                if (IsAnyDragging)
                {
                    IsAnyDragging = false;
                }
                LeaveDrag();
                
                footer.pickingMode = PickingMode.Ignore;
            }
            
            void EnterDrag()
            {
                if (!IsComponentDragging())
                    return;
                
                dragging = true;
                
                if (!IsAnyDragging)
                {
                    IsAnyDragging = true;
                    OnBeginDrag();
                }

                onPerformDrag += OnPerformDrag;
                
                LeaveOnLongHold();
                
                footer.visible = true;
                footer.pickingMode = PickingMode.Position;
            }
            void LeaveDrag()
            {
                dragging = false;
                footer.visible = false;
            }
        }
        
        static void _HandleDragPerformEvent()
        {
            onPerformDrag?.Invoke();
            onPerformDrag = null;
            IsAnyDragging = false;
        }

        static bool _UpdateInspectorVisibility()
        {
            // Skip default footer padding
            return false;
        }
        
        static bool IsComponentDragging()
        {
            var draggedObjects = DragAndDrop.objectReferences;
            
            if (draggedObjects.Length == 0)
                return false;
            if (draggedObjects.All(o => o is Component && !(o is Transform)))
                return true;
            if (draggedObjects.All(o => o is MonoScript))
                return true;
            
            return false;
        }

        static void GetTargetRect_(ref Rect __result)
        {
            __result.height = DragAreaHeight;
        }
        
        
        static void GetMarkerRect_(ref Rect __result
#if !UNITY_2020_3_OR_NEWER
        , float markerY, bool targetAbove
#endif
        )
        {
            __result.y = __result.yMax - DragMarkerHeight;
            __result.height = DragMarkerHeight;
            
#if UNITY_2020_1_OR_NEWER
            __result.y += DragMarkerY;
#endif
        }
    }
}