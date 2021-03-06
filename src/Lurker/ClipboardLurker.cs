﻿//-----------------------------------------------------------------------
// <copyright file="ClipboardLurker.cs" company="Wohs">
//     Missing Copyright information from a valid stylecop.json file.
// </copyright>
//-----------------------------------------------------------------------


namespace Lurker
{
    using Gma.System.MouseKeyHook;
    using Lurker.Helpers;
    using Lurker.Patreon.Events;
    using Lurker.Patreon.Models;
    using Lurker.Patreon.Parsers;
    using Lurker.Services;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using WindowsInput;
    using WK.Libraries.SharpClipboardNS;

    public class ClipboardLurker: IDisposable
    {
        #region Fields

        private InputSimulator _simulator;
        private PoeKeyboardHelper _keyboardHelper;
        private ItemParser _itemParser = new ItemParser();
        private SettingsService _settingsService;
        private SharpClipboard _clipboardMonitor;
        private string _lastClipboardText = string.Empty;
        private IKeyboardMouseEvents _keyboardEvent;
        private bool _globalClickBinded;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClipboardLurker"/> class.
        /// </summary>
        public ClipboardLurker(SettingsService settingsService, PoeKeyboardHelper keyboardHelper)
        {
            this.ClearClipboard();
            this._keyboardHelper = keyboardHelper;
            this._simulator = new InputSimulator();
            this._clipboardMonitor = new SharpClipboard();
            this._settingsService = settingsService;

            this._settingsService.OnSave += this.SettingsService_OnSave;
            this._clipboardMonitor.ClipboardChanged += this.ClipboardMonitor_ClipboardChanged;
            this._itemParser.CheckPledgeStatus();
            this.StartWatcher();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a new item is in the clipboard.
        /// </summary>
        public event EventHandler<PoeItem> Newitem;

        /// <summary>
        /// Creates new offer.
        /// </summary>
        public event EventHandler<string> NewOffer;

        #endregion

        #region Methods

        /// <summary>
        /// Binds the global click.
        /// </summary>
        public void BindGlobalClick()
        {
            if (this._globalClickBinded)
            {
                return;
            }

            this._globalClickBinded = true;
            this._keyboardEvent.MouseClick += this.KeyboardEvent_MouseClick;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._keyboardEvent.MouseClick -= this.KeyboardEvent_MouseClick;
                this._keyboardEvent.Dispose();
                this._clipboardMonitor.ClipboardChanged -= ClipboardMonitor_ClipboardChanged;
                this._settingsService.OnSave -= this.SettingsService_OnSave;
            }
        }


        /// <summary>
        /// Handles the OnSave event of the SettingsService control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void SettingsService_OnSave(object sender, EventArgs e)
        {
            await this._itemParser.CheckPledgeStatus();
            this.StopWatcher();
            this.StartWatcher();

            if (this._settingsService.SearchEnabled)
            {
                this.BindGlobalClick();
            }
            else
            {
                this._keyboardEvent.MouseClick -= this.KeyboardEvent_MouseClick;
                this._globalClickBinded = false;
            }
        }

        /// <summary>
        /// Starts the watcher.
        /// </summary>
        private void StartWatcher()
        {
            var search = Combination.FromString("Control+F");
            var remainingMonster = Combination.FromString("Control+R");
            var deleteItem = Combination.TriggeredBy(System.Windows.Forms.Keys.Delete);

            this._keyboardEvent = Hook.GlobalEvents();
            var assignment = new Dictionary<Combination, Action>
            {
                {search, this.Search},
                {remainingMonster, this.RemainingMonster},
                {deleteItem, this.DeleteItem},
            };

            this._keyboardEvent.OnCombination(assignment);
        }

        /// <summary>
        /// Stops the watcher.
        /// </summary>
        private void StopWatcher()
        {
            this._keyboardEvent.MouseClick -= this.KeyboardEvent_MouseClick;
            this._keyboardEvent.Dispose();
        }

        /// <summary>
        /// Handles the MouseClick event of the KeyboardEvent control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void KeyboardEvent_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!this._settingsService.SearchEnabled || e.Button != System.Windows.Forms.MouseButtons.Left)
            {
                return;
            }

            if (Native.IsKeyPressed(Native.VirtualKeyStates.VK_SHIFT) && Native.IsKeyPressed(Native.VirtualKeyStates.VK_CONTROL))
            {
                this.ParseItem();
            }
        }

        /// <summary>
        /// Gets the clipboard data.
        /// </summary>
        /// <returns>The clipboard text.</returns>
        private async Task<string> GetClipboardText()
        {
            this._simulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_C);
            await Task.Delay(20);

            var clipboardText = string.Empty;
            Thread thread = new Thread(() => 
            {
                var retryCount = 3;
                while (retryCount != 0)
                {
                    try
                    {
                        clipboardText = Clipboard.GetText();
                        break;
                    }
                    catch
                    {
                        retryCount--;
                        Thread.Sleep(200);
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return clipboardText;
        }

        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        private void ClearClipboard()
        {
            Thread thread = new Thread(() =>
            {
                var retryCount = 3;
                while (retryCount != 0)
                {
                    try
                    {
                        Clipboard.Clear();
                        break;
                    }
                    catch
                    {
                        retryCount--;
                        Thread.Sleep(200);
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        /// <summary>
        /// Handles the ClipboardChanged event of the ClipboardMonitor control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SharpClipboard.ClipboardChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="NotImplementedException"></exception>
        private void ClipboardMonitor_ClipboardChanged(object sender, SharpClipboard.ClipboardChangedEventArgs e)
        {
            if (e.ContentType == SharpClipboard.ContentTypes.Text)
            {
                var currentText = e.Content as string;
                if (string.IsNullOrEmpty(currentText))
                {
                    return;
                }

                if (this._lastClipboardText == currentText)
                {
                    return;
                }

                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    return;
                }

                var isTradeMessage = false;
                this._lastClipboardText = currentText;
                if (TradeEvent.IsTradeMessage(currentText))
                {
                    isTradeMessage = true;
                }
                else if (TradeEventHelper.IsTradeMessage(currentText))
                {
                    isTradeMessage = true;
                }

                if (isTradeMessage)
                {
                    this.NewOffer?.Invoke(this, currentText);
                }
            }
        }

        /// <summary>
        /// Parses the item.
        /// </summary>
        private async void ParseItem()
        {
            PoeItem item = default;
            var retryCount = 2;
            for (int i = 0; i < retryCount; i++)
            {
                item = await this.GetItemInClipboard();
                if (item == null)
                {
                    return;
                }

                if (!item.Identified)
                {
                    await Task.Delay(50);
                    continue;
                }

                break;
            }

            if (item == null || !item.Identified)
            {
                return;
            }

            this.Newitem?.Invoke(this, item);
            this.ClearClipboard();
        }

        /// <summary>
        /// Gets the item in clipboard.
        /// </summary>
        /// <returns>The item in the clipboard</returns>
        private async Task<PoeItem> GetItemInClipboard()
        {
            try
            {
                var text = await this.GetClipboardText();
                return this._itemParser.Parse(text);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the item base type in clipboard.
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetItemSearchValueInClipboard()
        {
            try
            {
                var text = await this.GetClipboardText();
                try
                {
                    return this._itemParser.GetSearchValue(text);
                }
                catch (System.InvalidOperationException)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Searches this instance.
        /// </summary>
        private async void Search()
        {
            if (this._settingsService.ItemHighlightEnabled && Models.PoeApplicationContext.InForeground)
            {
                var baseType = await this.GetItemSearchValueInClipboard();
                if (string.IsNullOrEmpty(baseType))
                {
                    return;
                }

                this._keyboardHelper.Write(baseType);
            }
        }

        /// <summary>
        /// Remainings the monster.
        /// </summary>
        private void RemainingMonster()
        {
            if (this._settingsService.RemainingMonsterEnabled && Models.PoeApplicationContext.InForeground)
            {
                this._keyboardHelper.RemainingMonster();
            }
        }

        /// <summary>
        /// Deletes the item.
        /// </summary>
        private void DeleteItem()
        {
            if (this._settingsService.DeleteItemEnabled && Models.PoeApplicationContext.InForeground)
            {
                this._simulator.Mouse.LeftButtonClick();
                this._keyboardHelper.Destroy();
            }
        }

        #endregion
    }
}
