using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Riten.Native.Cursors.Virtual;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riten.Native.Cursors.Tests
{
    public class AutoSetCursorPackPlayModeTests
    {
        private readonly List<GameObject> _objects = new();
        private readonly List<CursorPack> _packs = new();

        [UnityTest]
        public IEnumerator Providers_RestorePreviousOwnerWithoutClearingCurrentOwner()
        {
            var first = CreateProvider("First");
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(first));

            var second = CreateProvider("Second");
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(second));

            first.gameObject.SetActive(false);
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(second));

            first.gameObject.SetActive(true);
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(first));

            first.gameObject.SetActive(false);
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(second));

            second.gameObject.SetActive(false);
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.Null);
        }

        [UnityTest]
        public IEnumerator PublicCursorPackOverride_IsNotClearedByOldComponentOwner()
        {
            var component = CreateProvider("Component");
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(component));

            var publicPack = CreatePack();
            NativeCursor.SetCursorPack(publicPack, null);
            var publicOwner = GetCursorPackOwner();
            Assert.That(publicOwner, Is.Not.Null.And.Not.SameAs(component));

            component.gameObject.SetActive(false);
            yield return null;
            Assert.That(GetCursorPackOwner(), Is.SameAs(publicOwner));

            NativeCursor.ClearCursorPack();
            Assert.That(GetCursorPackOwner(), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NativeCursor.ClearCursorPack();

            foreach (var gameObject in _objects)
            {
                if (gameObject)
                    Object.Destroy(gameObject);
            }

            foreach (var pack in _packs)
            {
                if (pack)
                    Object.Destroy(pack);
            }

            _objects.Clear();
            _packs.Clear();
            yield return null;
        }

        private AutoSetCursorPack CreateProvider(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _objects.Add(gameObject);

            var provider = gameObject.AddComponent<AutoSetCursorPack>();
            SetPrivateField(provider, "_cursorPack", CreatePack());
            gameObject.SetActive(true);
            return provider;
        }

        private CursorPack CreatePack()
        {
            var pack = ScriptableObject.CreateInstance<CursorPack>();
            _packs.Add(pack);
            return pack;
        }

        private static object GetCursorPackOwner()
        {
            var property = typeof(NativeCursor).GetProperty(
                "CursorPackOwner",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
