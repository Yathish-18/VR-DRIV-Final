// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Editor
{
    public static class PGToolbarExtensions
    {
        /// <summary>
        ///     Hides the cavet (arrow) from the ToolbarMenu.
        /// </summary>
        /// <param name="centerText">Center the text of the TextElement.</param>
        /// <param name="hideText">Hide the text of the TextElement. Used when no text is set.</param>
        public static void PGRemoveMenuArrow(this ToolbarMenu toolbarMenu, bool centerText, bool hideText)
        {
            var toolbarText = toolbarMenu.Children().ToList()[0];
            if (centerText) toolbarText.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (hideText) toolbarText.style.display = DisplayStyle.None;
            var toolbarArrow = toolbarMenu.Children().ToList()[1];
            toolbarArrow.style.display = DisplayStyle.None;
        }

        public static void PGAppendMoveItems<T>(this ToolbarMenu toolbarMenu, IList<T> collection, int i, Action CreateEditorItems)
        {
            toolbarMenu.menu.AppendAction("Move Up", action =>
            {
                MoveItem(collection, i, i - 1);
                CreateEditorItems();
            });
            toolbarMenu.menu.AppendAction("Move Down", action =>
            {
                MoveItem(collection, i, i + 1);
                CreateEditorItems();
            });
            toolbarMenu.menu.AppendAction("Move Top", action =>
            {
                MoveItem(collection, i, 0);
                CreateEditorItems();
            });
            toolbarMenu.menu.AppendAction("Move Bottom", action =>
            {
                MoveItem(collection, i, collection.Count - 1);
                CreateEditorItems();
            });
        }
        
        private static void MoveItem<T>(IList<T> collection, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= collection.Count) return;
            if (newIndex < 0 || newIndex >= collection.Count) return;
            T removedItem = collection[oldIndex];
            collection.RemoveAt(oldIndex);
            collection.Insert(newIndex, removedItem);
        }
    }
}