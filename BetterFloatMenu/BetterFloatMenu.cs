using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EpicUtils
{
    /// <summary>
    /// An alternative to the built-in <see cref="FloatMenu"/>.
    /// Provides a more visual layout, and a search bar.
    /// </summary>
    public class BetterFloatMenu : Window
    {
        /// <summary>
        /// Opens a new float menu using the items provided.
        /// Note: by default, opening a new window will close existing windows.
        /// You should set <see cref="Window.onlyOneOfTypeAllowed"/> to false to disable this behaviour.
        /// </summary>
        /// <param name="items">The list of items that the user can choose from.</param>
        /// <param name="onSelected">The method to be called when an item is selected.</param>
        /// <returns>The newly created window.</returns>
        public static BetterFloatMenu Open(List<MenuItemBase> items, Action<MenuItemBase> onSelected)
        {
            var created = new BetterFloatMenu();
            created.Items = items;
            created.OnSelected = onSelected;
            created.closeOnAccept = false;
            created.closeOnCancel = true;
            created.closeOnClickedOutside = true;
            created.layer = WindowLayer.SubSuper;
            Find.WindowStack.Add(created);
            return created;
        }

        /// <summary>
        /// Generic string search and highlighting utility method.
        /// If it returns null, the search does not match the label.
        /// If it returns a string, the search succeeded. Furthermore, the return value will be a highlighted version of <paramref name="label"/> using RichText
        /// if the <paramref name="highlightColor"/> argument is not null, otherwise simply <paramref name="label"/>.
        /// </summary>
        /// <param name="label">The string to search in.</param>
        /// <param name="search">The search input.</param>
        /// <param name="highlightColor">The Hex format of the color to highlight with. Should be in the format #RRGGBB(AA). Can be null to disable highlighting.</param>
        /// <returns></returns>
        public static string SearchMatch(string label, string search, string highlightColor = "#65f065")
        {
            int index = label.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            if (highlightColor == null)
                return label;

            return label.Insert(index+search.Length, "</color>").Insert(index, $"<color={highlightColor}>");
        }

        /// <summary>
        /// A utility function to build a sorted list of <see cref="MenuItemBase"/>, to be used in <see cref="Open(List{MenuItemBase}, Action{MenuItemBase})"/>.
        /// Takes an numeration of 'raw items' and a function that converts those 'raw items' to <see cref="MenuItemBase"/>.
        /// </summary>
        /// <typeparam name="T">The type of the raw items.</typeparam>
        /// <param name="rawItems">An enumeration of raw items to build the menu items from.</param>
        /// <param name="makeItem">A function to build a menu item based on a raw item. If it returns a null item, it is ignored and will not be added to the final list.</param>
        /// <returns>A sorted list of menu items. Will not contain null values.</returns>
        public static List<MenuItemBase> MakeItems<T>(IEnumerable<T> rawItems, Func<T, MenuItemBase> makeItem)
        {
            var list = new List<MenuItemBase>();
            foreach (var item in rawItems)
            {
                var result = makeItem(item);
                if (result != null)
                    list.Add(result);
            }
            list.Sort();
            return list;
        }

        /// <summary>
        /// The list of items to display.
        /// </summary>
        public List<MenuItemBase> Items;
        /// <summary>
        /// Action called when an item is selected (clicked on).
        /// </summary>
        public Action<MenuItemBase> OnSelected;
        /// <summary>
        /// If true, the window will close after selecting an item.
        /// If false, <see cref="OnSelected"/> will be still be called but the window will not close.
        /// Default value: true.
        /// </summary>
        public bool CloseOnSelected = true;
        /// <summary>
        /// If true, displays a search bar that allows for items to be filtered out.
        /// </summary>
        public bool CanSearch = true;
        /// <summary>
        /// How many columns of items to display.
        /// Items are split equally between columns.
        /// The width of a column is determined by the widest item in the column.
        /// Default value: 2.
        /// </summary>
        public int Columns = 2;
        /// <summary>
        /// The amount of padding between items, measured in unscaled pixels.
        /// Default value: 6.
        /// </summary>
        public float Padding = 6;
        /// <summary>
        /// The current search string. Set to <see cref="string.Empty"/> or null to reset search bar.
        /// </summary>
        public string SearchString = "";

        private readonly List<MenuItemBase> preRenderItems = new List<MenuItemBase>();
        private float lastHeight, lastWidth;
        private Vector2 scroll;

        public override void DoWindowContents(Rect inRect)
        {
            SearchString ??= "";

            if (Items == null || Items.Count == 0)
            {
                Log.Message($"Opened a {nameof(BetterFloatMenu)} with no items! Closing...");
                Close(); 
                return;
            }

            // Draw search bar if allowed.
            if (CanSearch)
            {
                var searchBar = inRect;
                searchBar.height = 28;
                SearchString = Widgets.TextField(searchBar, SearchString);
                inRect.yMin += 36;
            }

            if (CanSearch || preRenderItems.Count != Items.Count)
            {
                preRenderItems.Clear();
                preRenderItems.AddRange(FilteredItems(SearchString));
            }

            int perColumnTarget = Mathf.CeilToInt((float)preRenderItems.Count / Columns);

            float x = 0;

            Widgets.BeginScrollView(inRect, ref scroll, new Rect(0, 0, lastWidth, lastHeight));
            lastWidth = 0;
            lastHeight = 0;

            for (int i = 0; i < Columns; i++)
            {
                float maxItemWidth = 0f;
                float y = 0;
                for (int j = 0; j < perColumnTarget; j++)
                {
                    int index = perColumnTarget * i + j;
                    if (index >= preRenderItems.Count)
                        break;

                    var pos = new Vector2(x, y);
                    var item = preRenderItems[index];
                    var size = item.Draw(pos);
                    var area = new Rect(pos, size);

                    if (item.BoxThickness > 0 && item.BoxColor.a > 0)
                    {
                        GUI.color = item.BoxColor;
                        Widgets.DrawBox(area, item.BoxThickness);
                        GUI.color = Color.white;
                    }

                    if (Widgets.ButtonInvisible(area))
                    {
                        OnSelected?.Invoke(item);
                        if (CloseOnSelected)
                        {
                            Close();
                            break;
                        }
                    }
                    y += size.y + Padding;
                    if (maxItemWidth < size.x)
                        maxItemWidth = size.x;
                    if (y > lastHeight)
                        lastHeight = y;
                }
                x += maxItemWidth + Padding * 2;
                if (x > lastWidth)
                    lastWidth = x;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// Returns the items that match the search string: the items that return true from <see cref="MenuItemBase.MatchesSearch(string)"/>.
        /// Passing in a null or blank <paramref name="search"/> string will return all items.
        /// </summary>
        /// <param name="search">The search string. May be null to return all items.</param>
        /// <returns>An enumeration of all items that match the search.</returns>
        public virtual IEnumerable<MenuItemBase> FilteredItems(string search)
        {
            if (Items == null)
                yield break;

            bool all = string.IsNullOrWhiteSpace(search);
            string newSearch = search?.Trim();

            foreach (var item in Items)
            {
                if (all || item.MatchesSearch(newSearch))
                    yield return item;
            }
        }
    }

    /// <summary>
    /// The base class for items displayed by a <see cref="BetterFloatMenu"/>.
    /// </summary>
    public abstract class MenuItemBase : IComparable<MenuItemBase>
    {
        /// <summary>
        /// User data.
        /// </summary>
        public object Payload { get; set; }
        /// <summary>
        /// The color of the containing box.
        /// </summary>
        public Color BoxColor = Color.white;
        /// <summary>
        /// The width, in pixels, of the containing box.
        /// </summary>
        public int BoxThickness = 1;

        /// <summary>
        /// Returns the <see cref="Payload"/>, cast to a specified type. May throw an invalid cast or null exception.
        /// Equivalent to: <code>(T)Payload</code>
        /// </summary>
        /// <typeparam name="T">The type to cast to.</typeparam>
        /// <returns>The payload, cast to a particular type.</returns>
        public T GetPayload<T>() => (T)Payload;

        /// <summary>
        /// Returns true if this item should be shown when searching for the <paramref name="search"/> string.
        /// </summary>
        /// <param name="search">The search string, that comes from the search bar.</param>
        /// <returns>True if this item should be shown, false to hide.</returns>
        public abstract bool MatchesSearch(string search);

        /// <summary>
        /// Used to sort this item within the window. Only called automatically when using <see cref="BetterFloatMenu.MakeItems{T}(IEnumerable{T}, Func{T, MenuItemBase})"/>.
        /// </summary>
        /// <param name="other">The other item to compare to.</param>
        public abstract int CompareTo(MenuItemBase other);

        /// <summary>
        /// When called should draw the item at the provided position.
        /// The position is the top-left corner. Should return the size, in pixels, that this item occupied.
        /// </summary>
        /// <param name="pos">The input position. This is the top-left, and it is in GUI space.</param>
        /// <returns>The size of this drawn item. Should not be negative.</returns>
        public abstract Vector2 Draw(Vector2 pos);
    }

    /// <summary>
    /// A <see cref="MenuItemText"/> that displays a label and optionally an icon next to it.
    /// Can also have a tooltip. Has a fixed size.
    /// </summary>
    public class MenuItemText : MenuItemBase
    {
        /// <summary>
        /// The label to display.
        /// </summary>
        public string Label;
        /// <summary>
        /// The tooltip to display. May be null.
        /// </summary>
        public string Tooltip;
        /// <summary>
        /// The icon to display. May be null.
        /// </summary>
        public Texture2D Icon;
        /// <summary>
        /// The tint of the icon.
        /// </summary>
        public Color IconColor = Color.white;
        /// <summary>
        /// The size of the box, in pixels.
        /// </summary>
        public Vector2 Size = new Vector2(212, 28);

        protected string drawLabel;
        private bool consumedSearch = false;

        public MenuItemText() { }

        public MenuItemText(object payload, string text, Texture2D icon = null, Color iconColor = default, string tooltip = null)
        {
            this.Payload = payload;
            this.Label = text;
            this.Icon = icon;
            this.Tooltip = tooltip;
            this.IconColor = iconColor == default ? Color.white : iconColor;
        }

        public override bool MatchesSearch(string search)
        {
            drawLabel = BetterFloatMenu.SearchMatch(Label, search);
            consumedSearch = false;
            return drawLabel != null;
        }

        public override int CompareTo(MenuItemBase other)
        {
            if (other is MenuItemText txt)
                return string.Compare(Label, txt.Label, StringComparison.Ordinal);
            return 0;
        }

        public override Vector2 Draw(Vector2 pos)
        {
            Rect area = new Rect(pos, Size);

            string label = Label;
            if (!consumedSearch)
            {
                label = drawLabel;
                consumedSearch = true;
            }

            bool hasIcon = Icon != null;

            if (hasIcon)
            {
                Rect iconArea = area;
                iconArea.width = iconArea.height;
                GUI.color = IconColor;
                Widgets.DrawTextureFitted(iconArea, Icon, 1f);
                GUI.color = Color.white;
            }

            Rect labelArea = area;
            labelArea.y += hasIcon ? 3 : 5;
            if (hasIcon)
                labelArea.xMin += area.height + 2;
            else
                labelArea.xMin += 4;
            
            Widgets.LabelFit(labelArea, label);

            if (Tooltip != null)
                TooltipHandler.TipRegion(area, Tooltip);

            return Size;
        }
    }

    /// <summary>
    /// A <see cref="MenuItemBase"/> that displays a single icon and no label. It is normally square.
    /// Can display a tooltip.
    /// </summary>
    public class MenuItemIcon : MenuItemBase
    {
        /// <summary>
        /// The size of the item. Defaults to a square 64x64.
        /// </summary>
        public Vector2 Size = new Vector2(64, 64);
        /// <summary>
        /// The optional tooltip text. Used to filter searches if provided.
        /// </summary>
        public string Tooltip;
        /// <summary>
        /// The icon to display.
        /// </summary>
        public Texture2D Icon;
        /// <summary>
        /// The tint of the icon.
        /// </summary>
        public Color Color = Color.white;
        /// <summary>
        /// The background color to place behind the icon. Defaults to (0, 0, 0, 0) i.e. no background.
        /// </summary>
        public Color BGColor = default;

        protected string drawLabel;
        private bool consumedSearch;

        public MenuItemIcon() { }

        public MenuItemIcon(object payload, string tooltip, Texture2D icon, Color iconColor = default)
        {
            this.Payload = payload;
            this.Tooltip = tooltip;
            this.Icon = icon;
            this.Color = iconColor == default ? Color.white : iconColor;
        }

        public override bool MatchesSearch(string search)
        {
            if (Tooltip == null)
                return true;

            consumedSearch = false;
            drawLabel = BetterFloatMenu.SearchMatch(Tooltip, search, null);
            return drawLabel != null;
        }

        public override int CompareTo(MenuItemBase other)
        {
            return 0; // No order, sort by natural load order (mod).
        }

        public override Vector2 Draw(Vector2 pos)
        {
            if (Icon == null)
                return Size;

            Rect area = new Rect(pos, Size);

            if (BGColor != default)
            {
                Widgets.DrawBoxSolid(area, BGColor);
            }

            var old = GUI.color;
            if(Color != Color.white)
                GUI.color = Color;
            Widgets.DrawTextureFitted(area, Icon, 1f);
            GUI.color = old;

            string label = Tooltip;
            if (!consumedSearch)
            {
                label = drawLabel;
                consumedSearch = true;
            }

            GUI.color = Color.white;
            TooltipHandler.TipRegion(area, label);
            GUI.color = old;

            return Size;
        }
    }
}
