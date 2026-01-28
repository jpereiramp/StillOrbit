using UnityEngine;

namespace Shooter
{
    public class MuzzleFlashController : MonoBehaviour
    {
        public GameObject[] muzzleFlashPrefabs; // Array of prefabs
        public Transform[] muzzleFlashTransforms; // Array of transforms for each prefab
        public float fireRate = 0.2f;
        private float muzzleFlashDuration = 0.4f;

        private float nextFireTime;

        void Start()
        {
            nextFireTime = Time.time;
        }

        void Update()
        {
            if (Time.time >= nextFireTime)
            {
                for (int i = 0; i < muzzleFlashPrefabs.Length; i++)
                {
                    // Create and destroy each prefab at its corresponding transform
                    GameObject muzzleFlash = Instantiate(muzzleFlashPrefabs[i], muzzleFlashTransforms[i].position, muzzleFlashTransforms[i].rotation);
                    Destroy(muzzleFlash, muzzleFlashDuration);
                }

                nextFireTime = Time.time + fireRate;
            }
        }
    }
}