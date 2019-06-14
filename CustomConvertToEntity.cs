using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static MethodInfo injectOriginalComponentsReturnBool;
        private static MethodInfo injectOriginalComponents;
        private static MethodInfo addRecurse;
        public bool hasRun;

        void Awake()
        {
            if (hasRun) return;
            gameObjectToConvertedEntity        = typeof(GameObjectConversionUtility).GetMethod("GameObjectToConvertedEntity", BindingFlags.NonPublic | BindingFlags.Static);
            createConversionWorld              = typeof(GameObjectConversionUtility).GetMethod("CreateConversionWorld", BindingFlags.NonPublic       | BindingFlags.Static);
            convert                            = typeof(GameObjectConversionUtility).GetMethod("Convert", BindingFlags.NonPublic                     | BindingFlags.Static);
            injectOriginalComponentsReturnBool = typeof(ConvertToEntity).GetMethods().FirstOrDefault(x => x.Name == "InjectOriginalComponents" && x.ReturnType == typeof(bool));
            injectOriginalComponents           = typeof(ConvertToEntity).GetMethod("InjectOriginalComponents", BindingFlags.NonPublic | BindingFlags.Static);
            addRecurse                         = typeof(ConvertToEntity).GetMethod("AddRecurse", BindingFlags.NonPublic               | BindingFlags.Static);

            var       gameObjectName = gameObject.name;
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
                        {
                            hasRun = true;
                            gameObject.GetComponent<CustomConvertToEntity>().enabled = false;
                            var tempGameObject = Instantiate(gameObject);
                            tempGameObject.name = gameObjectName + " (" + world + ")";
                            ConvertAndInjectOriginal(tempGameObject, convertToWorld);
                        }
                    }
                }
                Destroy(gameObject);
            }
            else
            {
                UnityEngine.Debug.LogWarning("ConvertEntity failed because there was no Active World", this);
            }
        }

        public static void ConvertHierarchy(GameObject root, World convertToWorld)
        {
            var gameObjectWorld = (World) createConversionWorld.Invoke(null, new object[] {convertToWorld, default(Hash128), GameObjectConversionUtility.ConversionFlags.AssignName});

            addRecurse.Invoke(null, new object[] { gameObjectWorld.EntityManager, root.transform });

            convert.Invoke(null, new object[] {gameObjectWorld, convertToWorld});

            injectOriginalComponentsReturnBool.Invoke(null, new object[] { gameObjectWorld, convertToWorld.EntityManager, root.transform });

            gameObjectWorld.Dispose();
        }


        public static void ConvertAndInjectOriginal(GameObject root, World convertToWorld)
        {
            var gameObjectWorld = (World)createConversionWorld.Invoke(null, new object[] { convertToWorld, default(Hash128), GameObjectConversionUtility.ConversionFlags.AssignName });

            GameObjectEntity.AddToEntityManager(gameObjectWorld.EntityManager, root);

            convert.Invoke(null, new object[] { gameObjectWorld, convertToWorld });

            var entity = (Entity)gameObjectToConvertedEntity.Invoke(null, new object[] { gameObjectWorld, root });
            injectOriginalComponents.Invoke(null, new object[] { convertToWorld.EntityManager, entity, root.transform });

            gameObjectWorld.Dispose();
        }
    }
}
