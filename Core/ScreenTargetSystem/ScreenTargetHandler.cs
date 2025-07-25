﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace Umbra.Core
{
	internal class ScreenTargetHandler : ModSystem, IOrderedLoadable
	{
		public static List<ScreenTarget> targets = new();
		public static Semaphore targetSem = new(1, 1);

		private static int firstResizeTime = 0;
		private static bool wasIngame;

		public float Priority => 1;

		new public void Load() //We want to use IOrderedLoadable's load here to preserve our load order
		{
			if (!Main.dedServ)
			{
				On_Main.CheckMonoliths += RenderScreens;
				Main.OnResolutionChanged += ResizeScreens;
				On_Main.UpdateMenu += MenuUpdate;
			}
		}

		new public void Unload()
		{
			if (!Main.dedServ)
			{
				On_Main.CheckMonoliths -= RenderScreens;
				Main.OnResolutionChanged -= ResizeScreens;

				Main.QueueMainThreadAction(() =>
				{
					if (targets != null)
					{
						targets.ForEach(n => n.RenderTarget?.Dispose());
						targets.Clear();
						targets = null;
					}
					else
					{
						Mod.Logger.Warn("Screen targets was null, all ScreenTargets may not have been released! (leaking VRAM!)");
					}
				});
			}
		}

		/// <summary>
		/// Registers a new screen target and orders it into the list. Called automatically by the constructor of ScreenTarget!
		/// </summary>
		/// <param name="toAdd"></param>
		public static void AddTarget(ScreenTarget toAdd)
		{
			targetSem.WaitOne();

			targets.Add(toAdd);
			targets.Sort((a, b) => a.order.CompareTo(b.order));

			targetSem.Release();
		}

		/// <summary>
		/// Removes a screen target from the targets list. Should not normally need to be used.
		/// </summary>
		/// <param name="toRemove"></param>
		public static void RemoveTarget(ScreenTarget toRemove)
		{
			targetSem.WaitOne();

			targets.Remove(toRemove);
			targets.Sort((a, b) => a.order - b.order > 0 ? 1 : -1);

			targetSem.Release();
		}

		public static void ResizeScreens(Vector2 obj)
		{
			if (Main.dedServ)
				return;

			targetSem.WaitOne();

			targets.ForEach(n =>
			{
				if (!Main.gameMenu || n.allowOnMenu)
				{
					Vector2? size = obj;

					if (n.onResize != null)
						size = n.onResize(obj);

					if (Main.gameMenu)
					{
						float menuScalingFactor = (float)size.Value.Y / 900f;
						if (menuScalingFactor < 1f)
							menuScalingFactor = 1f;

						if (Main.SettingDontScaleMainMenuUp)
							menuScalingFactor = 1f;

						size /= menuScalingFactor;
					}

					if (size != null)
					{
						n.RenderTarget?.Dispose();
						n.RenderTarget = new RenderTarget2D(Main.instance.GraphicsDevice, (int)size?.X, (int)size?.Y, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
					}
				}
			});

			targetSem.Release();
		}

		private void RenderScreens(On_Main.orig_CheckMonoliths orig)
		{
			orig();

			if (Main.dedServ)
				return;

			RenderTargetBinding[] bindings = Main.graphics.GraphicsDevice.GetRenderTargets();

			targetSem.WaitOne();

			foreach (ScreenTarget target in targets)
			{
				if (Main.gameMenu && !target.allowOnMenu)
					continue;

				if (target.drawFunct is null) //allows for RTs which dont draw in the default loop, like the lighting tile buffers
					continue;

				if (target.activeFunct())
				{
					Main.spriteBatch.Begin(default, default, Main.DefaultSamplerState, default, RasterizerState.CullNone, default);
					Main.graphics.GraphicsDevice.SetRenderTarget(target.RenderTarget);
					Main.graphics.GraphicsDevice.Clear(Color.Transparent);

					target.drawFunct(Main.spriteBatch);

					Main.spriteBatch.End();
				}
			}

			Main.graphics.GraphicsDevice.SetRenderTargets(bindings);

			targetSem.Release();
		}

		public override void PostUpdateEverything()
		{
			if (!wasIngame)
			{
				firstResizeTime = 0;
				wasIngame = true;
			}
			else
			{
				firstResizeTime++;
			}

			if (firstResizeTime == 20)
				ResizeScreens(new Vector2(Main.screenWidth, Main.screenHeight));
		}

		private void MenuUpdate(On_Main.orig_UpdateMenu orig)
		{
			if (wasIngame)
			{
				firstResizeTime = 0;
				wasIngame = false;
			}
			else
			{
				firstResizeTime++;
			}

			if (firstResizeTime == 20)
				ResizeScreens(new Vector2(Main.screenWidth, Main.screenHeight));

			orig();
		}
	}
}