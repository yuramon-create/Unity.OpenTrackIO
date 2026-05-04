using System;
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
                camera.usePhysicalProperties = true;

                if (packet.lens != null && packet.@static != null && packet.@static.camera != null)
                {
                    var staticCamera = packet.@static.camera;
                    float focalLength_mm = packet.lens.pinholeFocalLength; // in mm
                    float sensorWidth_mm = staticCamera.activeSensorPhysicalDimensions != null ? staticCamera.activeSensorPhysicalDimensions.width : 0f;
                    float sensorHeight_mm = staticCamera.activeSensorPhysicalDimensions != null ? staticCamera.activeSensorPhysicalDimensions.height : 0f;

                    if (sensorWidth_mm > 0f && sensorHeight_mm > 0f)
                    {
                        camera.sensorSize = new Vector2(sensorWidth_mm, sensorHeight_mm);
                    }

                    if (focalLength_mm > 0f)
                    {
                        camera.focalLength = focalLength_mm;
                    }

                    if (focalLength_mm > 0f && sensorHeight_mm > 0f)
                    {
                        float verticalFOV = 2f * Mathf.Atan(sensorHeight_mm / (2f * focalLength_mm)) * Mathf.Rad2Deg;
                        camera.fieldOfView = verticalFOV;
                    }

                    if (packet.lens.fStop > 0f)
                    {
                        TrySetCameraProperty(camera, "aperture", packet.lens.fStop);
                    }

                    if (staticCamera.isoSpeed > 0)
                    {
                        TrySetCameraProperty(camera, "iso", staticCamera.isoSpeed);
                    }

                    float shutterSpeed = GetShutterSpeed(staticCamera, staticCamera.captureFrameRate);
                    if (shutterSpeed > 0f)
                    {
                        TrySetCameraProperty(camera, "shutterSpeed", shutterSpeed);
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

        private static float GetShutterSpeed(Packet.Camera camera, Packet.Rational captureFrameRate)
        {
            if (camera == null || captureFrameRate == null || captureFrameRate.denom == 0) return 0f;

            float fps = (float)captureFrameRate.num / captureFrameRate.denom;
            if (fps <= 0f || camera.shutterAngle <= 0f) return 0f;

            return (camera.shutterAngle / 360f) / fps;
        }

        private static void TrySetCameraProperty(Camera camera, string propertyName, object value)
        {
            var property = typeof(Camera).GetProperty(propertyName);
            if (property == null || !property.CanWrite) return;

            var targetType = property.PropertyType;
            try
            {
                if (targetType == value.GetType())
                {
                    property.SetValue(camera, value);
                }
                else if (targetType == typeof(float))
                {
                    property.SetValue(camera, Convert.ToSingle(value));
                }
                else if (targetType == typeof(int))
                {
                    property.SetValue(camera, Convert.ToInt32(value));
                }
            }
            catch
            {
                // Ignore property assignment failures for unsupported API versions.
            }
        }
    }

}