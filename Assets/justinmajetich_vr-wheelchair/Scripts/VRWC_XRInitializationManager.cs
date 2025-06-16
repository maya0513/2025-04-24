using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace JustinMajetich.VRWheelchair
{
    public class VRWC_XRInitializationManager : MonoBehaviour
    {
        public static bool IsXRFullyInitialized { get; private set; } = false;
        
        [Header("Initialization Settings")]
        [SerializeField] private float stabilizationDelay = 0.5f;
        [SerializeField] private float maxInitializationTimeout = 30f;
        
        private void Start()
        {
            StartCoroutine(WaitForFullXRInitialization());
        }
        
        private IEnumerator WaitForFullXRInitialization()
        {
            float startTime = Time.time;
            
            // Wait for XR Settings to be enabled
            yield return new WaitUntil(() => 
            {
                if (Time.time - startTime > maxInitializationTimeout)
                {
                    Debug.LogError("XR Settings initialization timeout exceeded");
                    return true;
                }
                return XRSettings.enabled && XRSettings.loadedDeviceName != "None";
            });
            
            if (Time.time - startTime > maxInitializationTimeout)
            {
                Debug.LogError("Failed to initialize XR Settings within timeout");
                yield break;
            }
            
            // Force Input Device enumeration
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            
            Debug.Log($"Found {devices.Count} input devices during enumeration");
            
            // Wait for both hands to be valid
            startTime = Time.time;
            yield return new WaitUntil(() =>
            {
                if (Time.time - startTime > maxInitializationTimeout)
                {
                    Debug.LogWarning("Hand tracking initialization timeout - proceeding anyway");
                    return true;
                }
                
                bool leftHandValid = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid;
                bool rightHandValid = InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
                
                return leftHandValid && rightHandValid;
            });
            
            // Additional stabilization wait
            yield return new WaitForSeconds(stabilizationDelay);
            
            IsXRFullyInitialized = true;
            Debug.Log("XR initialization completed successfully");
        }
        
        public static void ResetInitialization()
        {
            IsXRFullyInitialized = false;
        }
    }
}