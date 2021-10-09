﻿#region License
/*
Copyright © Joan Charmant 2021.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;
using Kinovea.Video;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Interactive filter to create Kinograms.
    /// A Kinogram is a single image containing copies of the original frames of the video.
    /// 
    /// The parameters let the user change the subset of frames selected, the crop dimension,
    /// the number of columns and rows of the final composition, etc.
    /// 
    /// The filter is interactive. Changing the timestamp will shift the start time of the frames,
    /// panning in the viewport will pan inside the tile under the mouse.
    /// </summary>
    public class VideoFilterKinogram : IVideoFilter
    {
        #region Properties
        public Bitmap Current
        {
            get { return bitmap; }
        }
        public List<ToolStripItem> ContextMenu
        {
            get
            {
                // Just in time localization.
                mnuConfigure.Text = ScreenManagerLang.Generic_ConfigurationElipsis;
                mnuAutoNumbers.Text = "Frame numbers";
                mnuGenerateNumbers.Text = "Generate frame numbers";
                mnuDeleteNumbers.Text = "Delete frame numbers";
                mnuResetPositions.Text = "Reset positions";
                return contextMenu;
            }
        }
        public bool CanExportVideo
        {
            get { return false; }
        }

        public bool CanExportImage
        {
            get { return true; }
        }
        public KinogramParameters Parameters
        {
            get { return parameters; }
        }
        public int ContentHash
        {
            get { return parameters.GetContentHash(); }
        }
        #endregion

        #region members
        private Bitmap bitmap;
        private Size frameSize;
        private KinogramParameters parameters = new KinogramParameters();
        private IWorkingZoneFramesContainer framesContainer;
        private Metadata metadata;
        private long timestamp;
        private Color BackgroundColor = Color.FromArgb(44, 44, 44);
        bool clamp = false;
        private int movingTile = -1;
        private List<ToolStripItem> contextMenu = new List<ToolStripItem>();
        private ToolStripMenuItem mnuConfigure = new ToolStripMenuItem();
        private ToolStripMenuItem mnuAutoNumbers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuGenerateNumbers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDeleteNumbers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuResetPositions = new ToolStripMenuItem();
        #endregion

        #region ctor/dtor
        public VideoFilterKinogram(Metadata metadata)
        {
            this.metadata = metadata;
            
            mnuConfigure.Image = Properties.Drawings.configure;
            mnuAutoNumbers.Image = Properties.Drawings.number;
            mnuGenerateNumbers.Image = Properties.Drawings.number;
            mnuDeleteNumbers.Image = Properties.Resources.bin_empty;
            mnuResetPositions.Image = Properties.Resources.bin_empty;

            mnuAutoNumbers.DropDownItems.Add(mnuGenerateNumbers);
            mnuAutoNumbers.DropDownItems.Add(mnuDeleteNumbers);

            contextMenu.Add(mnuConfigure);
            contextMenu.Add(mnuAutoNumbers);
            contextMenu.Add(mnuResetPositions);
            contextMenu.Add(new ToolStripSeparator());

            mnuConfigure.Click += MnuConfigure_Click;
            mnuGenerateNumbers.Click += MnuAutonumbers_Click;
            mnuDeleteNumbers.Click += MnuDeleteAutoNumbers_Click;
            mnuResetPositions.Click += MnuResetPositions_Click;

            parameters = PreferencesManager.PlayerPreferences.Kinogram;
            AfterTileCountChange();
        }

        ~VideoFilterKinogram()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (bitmap != null)
                    bitmap.Dispose();
            }
        }
        #endregion

        #region IVideoFilter methods
        
        public void SetFrames(IWorkingZoneFramesContainer framesContainer)
        {
            // Changing the number of frames in the source doesn't impact the grid arrangement.
            // If we don't have enough frames we just show black tiles.
            this.framesContainer = framesContainer;
            if (framesContainer != null && framesContainer.Frames != null && framesContainer.Frames.Count > 0)
            {
                frameSize = framesContainer.Frames[0].Image.Size;
                UpdateSize(frameSize);
            }
        }

        public void UpdateSize(Size size)
        {
            if (bitmap == null || bitmap.Size != size)
            {
                if (bitmap != null)
                    bitmap.Dispose();

                bitmap = new Bitmap(size.Width, size.Height);
            }

            Update();
        }

        public void UpdateTime(long timestamp)
        {
            if (timestamp == this.timestamp)
                return;

            this.timestamp = timestamp;
            Update();
        }

        public void StartMove(PointF p)
        {
            movingTile = GetTile(p);
        }

        public void StopMove()
        {
            movingTile = -1;
            SaveAsDefaultParameters();
        }

        public void Move(float dx, float dy, Keys modifiers)
        {
            if (movingTile < 0)
                return;

            if ((modifiers & Keys.Shift) == Keys.Shift)
            {
                for (int i = 0; i < parameters.TileCount; i++)
                    MoveTile(dx, dy, i);
                
                Update();
            }
            else
            {
               MoveTile(dx, dy, movingTile);
               Update(movingTile);
            }
        }

        public void ExportVideo(IDrawingHostView host)
        {
            throw new NotImplementedException();
        }

        public void ExportImage(IDrawingHostView host)
        {
            // Launch dialog.
            FormExportKinogram fek = new FormExportKinogram(this, host.CurrentTimestamp);
            FormsHelper.Locate(fek);
            fek.ShowDialog();
            fek.Dispose();

            Update();
        }

        public void ResetData()
        {
            this.framesContainer = null;
            this.parameters = PreferencesManager.PlayerPreferences.Kinogram;
            AfterTileCountChange();
        }

        public void WriteData(XmlWriter w)
        {
            parameters.WriteXml(w);
        }

        public void ReadData(XmlReader r)
        {
            parameters.ReadXml(r);
            AfterTileCountChange();
            Update();
        }
        #endregion

        #region Public methods, called from dialogs.
        /// <summary>
        /// Paint the composite + annotations on a new bitmap at the requested size and return it.
        /// </summary>
        public Bitmap Export(Size outputSize, long timestamp)
        {
            Bitmap bitmap = new Bitmap(outputSize.Width, outputSize.Height);
            Graphics g = Graphics.FromImage(bitmap);
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            Paint(g, outputSize, false);

            // Annotations are expressed in the original frames coordinate system.
            Rectangle fitArea = UIHelper.RatioStretch(outputSize, frameSize);
            float scale = (float)outputSize.Width / fitArea.Width;
            Point location = new Point((int)(-fitArea.X * scale), (int)(-fitArea.Y * scale));

            MetadataRenderer metadataRenderer = new MetadataRenderer(metadata, true);
            metadataRenderer.Render(g, location, scale, timestamp);

            return bitmap;
        }

        /// <summary>
        /// Returns the aspect ratio of the kinogram.
        /// </summary>
        public float GetAspectRatio()
        {
            int cols = (int)Math.Ceiling((float)parameters.TileCount / parameters.Rows);
            Size cropSize = GetCropSize();
            Size fullSize = new Size(cropSize.Width * cols, cropSize.Height * parameters.Rows);
            return (float)fullSize.Width / fullSize.Height;
        }

        /// <summary>
        /// Return the real tile count clamped to the number of available frames.
        /// </summary>
        public int GetTileCount(int tileCount)
        {
            return Math.Min(framesContainer.Frames.Count, tileCount);
        }

        /// <summary>
        /// Return the average interval in seconds between adjacent frames of the kinogram.
        /// </summary>
        public float GetFrameInterval(int tileCount)
        {
            int maxFrames = Math.Min(framesContainer.Frames.Count, tileCount);
            float intervalFrames = (float)framesContainer.Frames.Count / maxFrames;
            float intervalTimestamp = intervalFrames * metadata.AverageTimeStampsPerFrame;
            float intervalSeconds = (float)(intervalTimestamp/ metadata.AverageTimeStampsPerSecond);
            return intervalSeconds;
        }
        #endregion

        #region Private methods
        private void MnuConfigure_Click(object sender, EventArgs e)
        {
            // Launch dialog.
            FormConfigureKinogram fck = new FormConfigureKinogram(this);
            FormsHelper.Locate(fck);
            fck.ShowDialog();

            if (fck.DialogResult == DialogResult.OK)
            {
                parameters = fck.Parameters.Clone();
                AfterTileCountChange();
                SaveAsDefaultParameters();
            }

            fck.Dispose();
            Update();

            InvalidateFromMenu(sender);
        }

        private void MnuResetPositions_Click(object sender, EventArgs e)
        {
            ResetCropPositions();
            SaveAsDefaultParameters();
            Update();

            InvalidateFromMenu(sender);
        }

        private void MnuAutonumbers_Click(object sender, EventArgs e)
        {
            // Reset the auto-numbers to be into the tiles.
            Size outputSize = bitmap.Size;
            int cols = (int)Math.Ceiling((float)parameters.TileCount / parameters.Rows);
            Size cropSize = GetCropSize();
            Size fullSize = new Size(cropSize.Width * cols, cropSize.Height * parameters.Rows);
            Rectangle paintArea = UIHelper.RatioStretch(fullSize, outputSize);
            Size tileSize = new Size(paintArea.Width / cols, paintArea.Height / parameters.Rows);
            int tileCount = Math.Min(framesContainer.Frames.Count, parameters.TileCount);
            
            List<PointF> numbers = new List<PointF>();
            for (int i = 0; i < tileCount; i++)
            {
                // Find the destination rectangle of this tile.
                Rectangle destRect = GetDestinationRectangle(i, cols, parameters.Rows, parameters.LeftToRight, paintArea, tileSize);

                // Anchor in the top-left by default. 
                // The user can move all the numbers at once later with SHIFT+drag.
                PointF location = new PointF(destRect.X + 10, destRect.Y + 10);
                numbers.Add(location);
            }

            metadata.AutoNumberManager.Configure(timestamp, metadata.AverageTimeStampsPerFrame, numbers);

            InvalidateFromMenu(sender);
        }

        private void MnuDeleteAutoNumbers_Click(object sender, EventArgs e)
        {
            List<PointF> numbers = new List<PointF>();
            metadata.AutoNumberManager.Configure(timestamp, metadata.AverageTimeStampsPerFrame, numbers);

            InvalidateFromMenu(sender);
        }

        /// <summary>
        /// Add or remove crop positions slots after a change in the number of tiles.
        /// parameters.TileCount has the new number of tiles, 
        /// parameters.CropPositions has the old list of positions.
        /// </summary>
        private void AfterTileCountChange()
        {
            int oldCount = parameters.CropPositions.Count;
            int newCount = parameters.TileCount;

            if (newCount == oldCount)
                return;

            // Interpolate the new positions to match the existing motion of the tiles within the scene.
            List<PointF> newCrops = new List<PointF>();
            for (int i = 0; i < newCount; i++)
            {
                // Find the two closest old values and where we sit between them.
                float t = ((float)i / newCount) * oldCount;
                int a = (int)Math.Floor(t);
                int b = Math.Min(a + 1, oldCount - 1);
                float alpha = t - a;
                float beta = 1.0f - alpha;
                float x = parameters.CropPositions[a].X * beta + parameters.CropPositions[b].X * alpha;
                float y = parameters.CropPositions[a].Y * beta + parameters.CropPositions[b].Y * alpha;
                newCrops.Add(new PointF(x, y));
            }

            parameters.CropPositions = newCrops;
        }

        #region Rendering
        /// <summary>
        /// Paint the composite or one tile on the internal bitmap.
        /// This is used for the viewport rendering.
        /// </summary>
        private void Update(int tile = -1)
        {
            if (bitmap == null || framesContainer == null || framesContainer.Frames == null || framesContainer.Frames.Count < 1)
                return;

            Graphics g = Graphics.FromImage(bitmap);
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            Paint(g, bitmap.Size, true, tile);
        }

        /// <summary>
        /// Paint the composite or paint one tile of the composite.
        /// </summary>
        private void Paint(Graphics g, Size outputSize, bool isViewport, int tile = -1)
        { 
            float step = (float)framesContainer.Frames.Count / parameters.TileCount;
            IEnumerable<VideoFrame> frames = framesContainer.Frames.Where((frame, i) => i % step < 1);

            int cols = (int)Math.Ceiling((float)parameters.TileCount / parameters.Rows);
            Size cropSize = GetCropSize();
            Size fullSize = new Size(cropSize.Width * cols, cropSize.Height * parameters.Rows);

            Rectangle paintArea = UIHelper.RatioStretch(fullSize, outputSize);
            Size tileSize = new Size(paintArea.Width / cols, paintArea.Height / parameters.Rows);

            VideoFrame highlight = null;
            if (isViewport)
            {
                // Find the tile matching the current time for highlight.
                foreach (VideoFrame f in frames)
                {
                    if (f.Timestamp > timestamp)
                        break;

                    highlight = f;
                }
            }
           
            if (tile >= 0)
            {
                // Render a single tile.
                int index = tile;
                VideoFrame f = frames.ToList()[index];
                RectangleF srcRect = new RectangleF(parameters.CropPositions[index].X, parameters.CropPositions[index].Y, cropSize.Width, cropSize.Height);
                Rectangle destRect = GetDestinationRectangle(index, cols, parameters.Rows, parameters.LeftToRight, paintArea, tileSize);
                using (SolidBrush b = new SolidBrush(parameters.BorderColor))
                    g.FillRectangle(b, destRect);

                g.DrawImage(f.Image, destRect, srcRect, GraphicsUnit.Pixel);
                DrawBorder(g, destRect);
                if (f == highlight)
                    DrawHighlight(g, destRect);
            }
            else
            {
                // Render the whole composite.
                using (SolidBrush backgroundBrush = new SolidBrush(BackgroundColor))
                    g.FillRectangle(backgroundBrush, 0, 0, outputSize.Width, outputSize.Height);
                
                int index = 0;
                foreach (VideoFrame f in frames)
                {
                    RectangleF srcRect = new RectangleF(parameters.CropPositions[index].X, parameters.CropPositions[index].Y, cropSize.Width, cropSize.Height);
                    Rectangle destRect = GetDestinationRectangle(index, cols, parameters.Rows, parameters.LeftToRight, paintArea, tileSize);

                    using (SolidBrush b = new SolidBrush(parameters.BorderColor))
                        g.FillRectangle(b, destRect);

                    g.DrawImage(f.Image, destRect, srcRect, GraphicsUnit.Pixel);
                    DrawBorder(g, destRect);
                    if (f == highlight)
                        DrawHighlight(g, destRect);
                    
                    index++;
                }
            }
        }

        private void DrawBorder(Graphics g, Rectangle rect)
        {
            if (!parameters.BorderVisible)
                return;
            
            using (Pen p = new Pen(parameters.BorderColor))
                g.DrawRectangle(p, new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1));
        }

        private void DrawHighlight(Graphics g, Rectangle rect)
        {
            using (Pen p = new Pen(Color.CornflowerBlue, 2.0f))
                g.DrawRectangle(p, new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1));
        }

        /// <summary>
        /// Returns the part of the target image where the passed tile should be drawn.
        /// </summary>
        private Rectangle GetDestinationRectangle(int index, int cols, int rows, bool ltr, Rectangle paintArea, Size tileSize)
        {
            int row = index / cols;
            int col = index - (row * cols);
            if (!ltr)
                col = (cols - 1) - col;

            return new Rectangle(paintArea.Left + col * tileSize.Width, paintArea.Top + row * tileSize.Height, tileSize.Width, tileSize.Height);
        }
        #endregion

        private void ResetCropPositions()
        {
            parameters.CropPositions.Clear();
            for (int i = 0; i < parameters.TileCount; i++)
                parameters.CropPositions.Add(PointF.Empty);
        }

        /// <summary>
        /// Save the configuration as the new preferred configuration.
        /// </summary>
        private void SaveAsDefaultParameters()
        {
            PreferencesManager.PlayerPreferences.Kinogram = parameters.Clone();
            PreferencesManager.Save();
        }

        /// <summary>
        /// Get the final crop size, taking the original frame size into account.
        /// </summary>
        private Size GetCropSize()
        {
            int cropWidth = parameters.CropSize.Width;
            int cropHeight = parameters.CropSize.Height;

            float aspect = (float)cropWidth / cropHeight;
            if (cropWidth > frameSize.Width)
            {
                cropWidth = frameSize.Width;
                cropHeight = (int)(cropWidth / aspect);
            }

            if (cropHeight > frameSize.Height)
            {
                cropHeight = frameSize.Height;
                cropWidth = (int)(cropHeight * aspect);
            }

            return new Size(cropWidth, cropHeight);
        }

        /// <summary>
        /// Find the tile under this point.
        /// The point is given in the space of the original cached images.
        /// </summary>
        private int GetTile(PointF p)
        {
            Size size = bitmap.Size;
            int cols = (int)Math.Ceiling((float)parameters.TileCount / parameters.Rows);
            Size fullSize = new Size(parameters.CropSize.Width * cols, parameters.CropSize.Height * parameters.Rows);
            Rectangle paintArea = UIHelper.RatioStretch(fullSize, size);
            Size tileSize = new Size(paintArea.Width / cols, paintArea.Height / parameters.Rows);

            // Express the coordinate in the paint area.
            p = new PointF(p.X - paintArea.X, p.Y - paintArea.Y);

            if (p.X < 0 || p.Y < 0 || p.X >= paintArea.Width || p.Y >= paintArea.Height)
                return -1;

            int col = (int)(p.X / tileSize.Width);
            if (!parameters.LeftToRight)
                col = (cols - 1) - col;

            int row = (int)(p.Y / tileSize.Height);
            int index = row * cols + col;

            if (index >= framesContainer.Frames.Count)
                return -1;

            return index;
        }

        /// <summary>
        /// Move the crop position of a specific tile.
        /// </summary>
        private void MoveTile(float dx, float dy, int index)
        {
            PointF old = parameters.CropPositions[index];
            float x = old.X - dx;
            float y = old.Y - dy;

            if (clamp)
            {
                Size cropSize = GetCropSize();
                x = Math.Max(0, x);
                x = Math.Min(frameSize.Width - cropSize.Width, x);
                y = Math.Max(0, y);
                y = Math.Min(frameSize.Height - cropSize.Height, y);
            }

            parameters.CropPositions[index] = new PointF(x, y);
        }

        private void InvalidateFromMenu(object sender)
        {
            // Update the main viewport.
            // The screen hook was injected inside the menu.
            ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
            if (tsmi == null)
                return;

            IDrawingHostView host = tsmi.Tag as IDrawingHostView;
            if (host == null)
                return;

            host.InvalidateFromMenu();
        }
        #endregion
    }
}
