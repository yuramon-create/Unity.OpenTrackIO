using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace OpenTrackIO
{
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class OpenTrackIOLink : MonoBehaviour
    {
        [Header("Listening port")]
        [Tooltip("Run the server in edit mode")]
        public new bool runInEditMode = true;
        [Tooltip("Receive OpenTrackIO packets on this UDP port")]
        public int port = 40_000;
        [Tooltip("Apply the values received to the gameObject")]
        public bool apply = true;
        OpenTrackIOServer server;
        Packet packet;
        public Packet lastPacket;
        void OnEnable()
        {
            if (!Application.isPlaying && !runInEditMode) return;
            server = OpenTrackIOServer.Get(port);
            server.received += (packet) =>
            {
                this.packet = packet;
            };
        }
        void Update() {
            if (!Application.isPlaying && !runInEditMode) return;
            if (packet == null) return;
            lastPacket = packet;
            if (!apply) return;
            
            // Apply the packet values to the transform and camera
            if (packet.transforms != null && packet.transforms.Length > 0)
            {
                var transformData = packet.transforms[0];
                // exchange for Unity coordinate system (Y up, Z forward, X right)
                transform.localPosition = new Vector3(
                    transformData.translation.x,
                    transformData.translation.z,
                    transformData.translation.y
                );

                // Convert pan, tilt, roll to Unity coordinate system (Y up, Z forward, X right)
                var sourceRotation =
                    Quaternion.AngleAxis(-transformData.rotation.pan, Vector3.up) *
                    Quaternion.AngleAxis(-transformData.rotation.tilt, Vector3.right) *
                    Quaternion.AngleAxis(-transformData.rotation.roll, Vector3.forward);

                var convert = Quaternion.Euler(0f, 0f, 0f);
                transform.localRotation = convert * sourceRotation;
            }

            var camera = GetComponent<Camera>();
            if (camera != null)
            {
                // Apply zoom and focus values to the camera
                if (packet.lens != null && packet.@static != null && packet.@static.camera != null && packet.@static.camera.activeSensorPhysicalDimensions != null)
                {
                    // Calculate FOV from sensor size and focal length (both in mm)
                    float focalLength_mm = packet.lens.pinholeFocalLength; // in mm
                    float sensorHeight_mm = packet.@static.camera.activeSensorPhysicalDimensions.height; // in mm
                    
                    if (focalLength_mm > 0 && sensorHeight_mm > 0)
                    {
                        // Vertical FOV in degrees: 2 * atan(sensorHeight / (2 * focalLength)) * (180 / PI)
                        float verticalFOV = 2f * Mathf.Atan(sensorHeight_mm / (2f * focalLength_mm)) * Mathf.Rad2Deg;
                        camera.fieldOfView = verticalFOV;
                    }
                }
                if (packet.lens != null)
                {
                    // focusDistance is in meters, Unity expects meters
                    camera.focusDistance = packet.lens.focusDistance;
                }
            }
                      
           
            packet = null;
        }
        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }
        void OnDisable()
        {
            if (server == null) return;

            server.Stop();

        }

    }

}