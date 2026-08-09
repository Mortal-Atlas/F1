using F1AR.VR;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEngine;

namespace F1AR.Editor
{
    /// <summary>
    /// Editor tool to attach Meta XR BuildingBlock components:
    /// - Oculus.Interaction.Grabbable
    /// - HandGrabInteractable
    /// - DistanceHandGrabInteractable
    /// - Rigidbody (isKinematic = true, useGravity = false for floating in place)
    /// - Collider (Convex MeshCollider / BoxCollider)
    /// - FloatingGrabbablePart
    /// to every individual CAD component of all vehicles in the scene (F1 Car, DeLorean, Pod Racer).
    /// </summary>
    public static class SetupGrabbableCADPartsTool
    {
        [MenuItem("Tools/F1 AR/Auto-Make All F1 Parts Grabbable in VR")]
        public static void MakeAllPartsGrabbable()
        {
            string[] targets = { "Car", "f4goto", "delorean", "pod", "pod_complet_low", "anakin", "astonmartin" };
            int totalUpdated = 0;

            foreach (string targetName in targets)
            {
                GameObject vehicleObj = GameObject.Find(targetName);
                if (vehicleObj == null) continue;

                Renderer[] renderers = vehicleObj.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer r in renderers)
                {
                    GameObject child = r.gameObject;

                    // 1. Ensure Collider exists
                    Collider col = child.GetComponent<Collider>();
                    if (col == null)
                    {
                        MeshFilter mf = child.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            MeshCollider mc = Undo.AddComponent<MeshCollider>(child);
                            mc.convex = true;
                        }
                        else
                        {
                            Undo.AddComponent<BoxCollider>(child);
                        }
                    }

                    // 2. Ensure Rigidbody exists for Meta Building Block Grabbable (Floating in place)
                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = Undo.AddComponent<Rigidbody>(child);
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    // 3. Attach Meta XR Building Block Grabbable Component
                    Grabbable grabbable = child.GetComponent<Grabbable>();
                    if (grabbable == null)
                    {
                        grabbable = Undo.AddComponent<Grabbable>(child);
                    }

                    // 4. Attach Meta XR HandGrabInteractable
                    HandGrabInteractable handGrab = child.GetComponent<HandGrabInteractable>();
                    if (handGrab == null)
                    {
                        handGrab = Undo.AddComponent<HandGrabInteractable>(child);
                        handGrab.InjectOptionalPointableElement(grabbable);
                        handGrab.InjectRigidbody(rb);
                    }

                    // 5. Attach Meta XR DistanceHandGrabInteractable (Ray / Controller Grab)
                    DistanceHandGrabInteractable distanceGrab = child.GetComponent<DistanceHandGrabInteractable>();
                    if (distanceGrab == null)
                    {
                        distanceGrab = Undo.AddComponent<DistanceHandGrabInteractable>(child);
                        distanceGrab.InjectOptionalPointableElement(grabbable);
                        distanceGrab.InjectRigidbody(rb);
                    }

                    // 6. Attach FloatingGrabbablePart for Snap Back B-button logic
                    FloatingGrabbablePart floatingPart = child.GetComponent<FloatingGrabbablePart>();
                    if (floatingPart == null)
                    {
                        floatingPart = Undo.AddComponent<FloatingGrabbablePart>(child);
                    }

                    totalUpdated++;
                }
            }

            // Ensure VRControllerInputManager and WheelSpinAnimationController are on Manager
            GameObject managerObj = GameObject.Find("Manager");
            if (managerObj == null)
            {
                managerObj = new GameObject("Manager");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create Manager");
            }

            if (managerObj.GetComponent<VRControllerInputManager>() == null)
            {
                Undo.AddComponent<VRControllerInputManager>(managerObj);
            }

            if (managerObj.GetComponent<WheelSpinAnimationController>() == null)
            {
                Undo.AddComponent<WheelSpinAnimationController>(managerObj);
            }

            Debug.Log($"🎯 [SetupGrabbableCADPartsTool] Successfully attached Meta XR Building Block Grabbable, HandGrabInteractable, DistanceHandGrabInteractable, Rigidbody, & FloatingGrabbablePart to {totalUpdated} CAD components across all vehicles (F1 Car, DeLorean, Pod Racer)!");
        }
    }
}
