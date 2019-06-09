using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities
{
    public class CustomConvertToEntity : MonoBehaviour
    {
        public List<string> CreateToWorld = new List<string>();

        public enum Mode
        {
            ConvertAndDestroy,
            ConvertAndInjectGameObject
        }

        public Mode ConversionMode;

        private static MethodInfo gameObjectToConvertedEntity;
        private static MethodInfo createConversionWorld;
        private static MethodInfo convert;

        void Awake()
        {
            gameObjectToConvertedEntity = typeof(GameObjectConversionUtility).GetMethod("GameObjectToConvertedEntity", BindingFlags.NonPublic | BindingFlags.Static);
            createConversionWorld       = typeof(GameObjectConversionUtility).GetMethod("CreateConversionWorld",       BindingFlags.NonPublic | BindingFlags.Static);
            convert                     = typeof(GameObjectConversionUtility).GetMethod("Convert",                     BindingFlags.NonPublic | BindingFlags.Static);

            if (World.Active != null && CreateToWorld.Count > 0)
            {
                // Root ConvertToEntity is responsible for converting the whole hierarchy
                if (transform.parent != null && transform.parent.GetComponentInParent<CustomConvertToEntity>() != null)
                    return;

                foreach (var world in CreateToWorld)
                {
                    var convertToWorld = World.AllWorlds.FirstOrDefault(x => x.Name == world);
                    if (convertToWorld != null)
                    {
                        if (ConversionMode == Mode.ConvertAndDestroy)
                            ConvertHierarchy(gameObject, convertToWorld);
                        else
                            ConvertAndInjectOriginal(gameObject, convertToWorld);
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("ConvertEntity failed because there was no Active World", this);
            }
        }

        static void InjectOriginalComponents(EntityManager entityManager, Entity entity, Transform transform)
        {
            foreach (var com in transform.GetComponents<Component>())
            {
                if (com is GameObjectEntity || com is CustomConvertToEntity || com is ComponentDataProxyBase)
                    continue;

                entityManager.AddComponentObject(entity, com);
            }
        }

        public static void AddRecurse(EntityManager manager, Transform transform)
        {
            GameObjectEntity.AddToEntityManager(manager, transform.gameObject);

            var convert = transform.GetComponent<CustomConvertToEntity>();
            if (convert != null && convert.ConversionMode == Mode.ConvertAndInjectGameObject)
                return;

            foreach (Transform child in transform)
                AddRecurse(manager, child);
        }

        public static bool InjectOriginalComponents(World srcGameObjectWorld, EntityManager simulationWorld, Transform transform)
        {
            var convert = transform.GetComponent<CustomConvertToEntity>();

            if (convert != null && convert.ConversionMode == Mode.ConvertAndInjectGameObject)
            {
                var entity = (Entity)gameObjectToConvertedEntity.Invoke(null, new object[] { srcGameObjectWorld, transform.gameObject});
                InjectOriginalComponents(simulationWorld, entity, transform);
                transform.parent = null;
                return true;
            }

            for (int i = 0; i < transform.childCount;)
            {
                if (!InjectOriginalComponents(srcGameObjectWorld, simulationWorld, transform.GetChild(i)))
                    i++;
            }

            return false;
        }

        public static void ConvertHierarchy(GameObject root, World convertToWorld)
        {
            var gameObjectWorld = (World)createConversionWorld.Invoke(null, new object[] {convertToWorld, default(Hash128), GameObjectConversionUtility.ConversionFlags.AssignName});

            AddRecurse(gameObjectWorld.EntityManager, root.transform);

            convert.Invoke(null, new object[] {gameObjectWorld, convertToWorld});

            InjectOriginalComponents(gameObjectWorld, convertToWorld.EntityManager, root.transform);

            GameObject.Destroy(root);

            gameObjectWorld.Dispose();
        }


        public static void ConvertAndInjectOriginal(GameObject root, World convertToWorld)
        {
            var gameObjectWorld = (World)createConversionWorld.Invoke(null, new object[] {convertToWorld, default(Hash128), GameObjectConversionUtility.ConversionFlags.AssignName});

            GameObjectEntity.AddToEntityManager(gameObjectWorld.EntityManager, root);

            convert.Invoke(null, new object[] {gameObjectWorld, convertToWorld});

            var entity = (Entity)gameObjectToConvertedEntity.Invoke(null, new object[] { gameObjectWorld, root });
            InjectOriginalComponents(convertToWorld.EntityManager, entity, root.transform);

            gameObjectWorld.Dispose();
        }
    }
}
