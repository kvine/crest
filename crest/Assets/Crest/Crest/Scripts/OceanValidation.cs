﻿// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// How to use:
// Create a custom editor that inherits from ValidatedEditor. Then implement IValidated on the component.

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Crest
{
    public interface IValidated
    {
        bool Validate(OceanRenderer ocean, ValidatedHelper.ShowMessage showMessage);
    }

    // Only used with help boxes since we want to group messages together.
    public struct ValidatedMessage
    {
        public string message;
        public MessageType type;
    }

    // Holds the shared list for messages
    public static class ValidatedHelper
    {
        // This is a shared list. It will be cleared before use. It is only used by the HelpBox delegate since we want
        // to group them by severity (MessageType).
        public static readonly List<ValidatedMessage> messages = new List<ValidatedMessage>();

        public delegate void ShowMessage(string message, MessageType type, Object @object = null);

        public static void DebugLog(string message, MessageType type, Object @object = null)
        {
            message = $"Validation: {message} Click this message to highlight the problem object.";

            switch (type)
            {
                case MessageType.Error: Debug.LogError(message, @object); break;
                case MessageType.Warning: Debug.LogWarning(message, @object); break;
                default: Debug.Log(message, @object); break;
            }
        }

        public static void HelpBox(string message, MessageType type, Object @object = null)
        {
            messages.Add(new ValidatedMessage { message = message, type = type });
        }
    }

    public abstract class ValidatedEditor : Editor
    {
        public void ShowValidationMessages()
        {
            IValidated target = (IValidated)this.target;

            // Enable rich text in help boxes.
            GUI.skin.GetStyle("HelpBox").richText = true;

            var messageTypes = System.Enum.GetValues(typeof(MessageType));

            // This is a static list so we need to clear it before use. Not sure if this will ever be a threaded
            // operation which would be an issue.
            ValidatedHelper.messages.Clear();

            // OceanRenderer isn't a hard requirement for validation to work. Null needs to be handled in each
            // component.
            target.Validate(FindObjectOfType<OceanRenderer>(), ValidatedHelper.HelpBox);

            // We only want space before and after the list of help boxes. We don't want space between.
            var needsSpaceAbove = true;
            var needsSpaceBelow = false;

            // We loop through in reverse order so errors appears at the top.
            for (var messageTypeIndex = messageTypes.Length - 1; messageTypeIndex >= 0; messageTypeIndex--)
            {
                var filtered = ValidatedHelper.messages.FindAll(x => (int) x.type == messageTypeIndex);
                if (filtered.Count > 0)
                {
                    if (needsSpaceAbove)
                    {
                        // Double space looks good at top.
                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        needsSpaceAbove = false;
                    }

                    needsSpaceBelow = true;

                    // We join the messages together to reduce vertical space since HelpBox has padding, borders etc.
                    var joinedMessage = filtered[0].message;
                    // Format as list if we have more than one message.
                    if (filtered.Count > 1) joinedMessage = $"- {joinedMessage}";

                    for (var messageIndex = 1; messageIndex < filtered.Count; messageIndex++)
                    {
                        joinedMessage += $"\n- {filtered[messageIndex].message}";
                    }

                    EditorGUILayout.HelpBox(joinedMessage, (MessageType)messageTypeIndex);
                }
            }

            if (needsSpaceBelow)
            {
                EditorGUILayout.Space();
            }

        }

        public override void OnInspectorGUI()
        {
            ShowValidationMessages();

            // Draw the normal inspector after validation messages.
            base.OnInspectorGUI();
        }
    }
}

#endif