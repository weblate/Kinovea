
/*
Copyright © Joan Charmant 2008.
joan.charmant@gmail.com 
 
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    [XmlType ("Angle")]
    public class DrawingAngle2D : AbstractDrawing, IKvaSerializable, IDecorable, IInitializable, ITrackable, IMeasurable
    {
        #region Events
        public event EventHandler<TrackablePointMovedEventArgs> TrackablePointMoved;
        public event EventHandler ShowMeasurableInfoChanged;
        #endregion
        
        #region Properties
        public override string DisplayName
        {
            get { return ScreenManagerLang.ToolTip_DrawingToolAngle2D; }
        }
        public override int ContentHash
        {
            get 
            { 
                int hash = points["o"].GetHashCode();
                hash ^= points["a"].GetHashCode();
                hash ^= points["b"].GetHashCode();
                hash ^= styleHelper.ContentHash;
                hash ^= infosFading.ContentHash;
                return hash; 
            }
        } 
        public DrawingStyle DrawingStyle
        {
            get { return style;}
        }
        public override InfosFading InfosFading
        {
            get{ return infosFading;}
            set{ infosFading = value;}
        }
        public override DrawingCapabilities Caps
        {
            get { return DrawingCapabilities.ConfigureColor | DrawingCapabilities.Fading | DrawingCapabilities.Track; }
        }
        public override List<ToolStripItem> ContextMenu
        {
            get 
            {
                // Rebuild the menu to get the localized text.
                List<ToolStripItem> contextMenu = new List<ToolStripItem>();
                
                mnuInvertAngle.Text = ScreenManagerLang.mnuInvertAngle;
                contextMenu.Add(mnuInvertAngle);
                
                return contextMenu; 
            }
        }
        public CalibrationHelper CalibrationHelper { get; set; }
        public bool ShowMeasurableInfo { get; set; }
        #endregion

        #region Members
        private Guid id = Guid.NewGuid();
        private Dictionary<string, Point> points = new Dictionary<string, Point>();
        private bool tracking;
        
        private AngleHelper angleHelper = new AngleHelper(false, 40, false, "");
        private DrawingStyle style;
        private StyleHelper styleHelper = new StyleHelper();
        private InfosFading infosFading;
        
        private ToolStripMenuItem mnuInvertAngle = new ToolStripMenuItem();
        
        private const int defaultBackgroundAlpha = 92;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor
        public DrawingAngle2D(Point o, Point a, Point b, long timestamp, long averageTimeStampsPerFrame, DrawingStyle stylePreset)
        {
            points.Add("o", o);
            points.Add("a", a);
            points.Add("b", b);
            ComputeValues();

            // Decoration and binding to mini editors.
            styleHelper.Bicolor = new Bicolor(Color.Empty);
            styleHelper.Font = new Font("Arial", 12, FontStyle.Bold);
            if(stylePreset != null)
            {
                style = stylePreset.Clone();
                BindStyle();    
            }
            
            // Fading
            infosFading = new InfosFading(timestamp, averageTimeStampsPerFrame);

            mnuInvertAngle.Click += mnuInvertAngle_Click;
            mnuInvertAngle.Image = Properties.Drawings.angleinvert;
        }
        public DrawingAngle2D(XmlReader xmlReader, PointF scale, Metadata parent)
            : this(Point.Empty, Point.Empty, Point.Empty, 0, 0, ToolManager.Angle.StylePreset.Clone())
        {
            ReadXml(xmlReader, scale);
        }
        #endregion

        #region AbstractDrawing Implementation
        public override void Draw(Graphics canvas, IImageToViewportTransformer transformer, bool selected, long currentTimestamp)
        {
            double opacityFactor = infosFading.GetOpacityFactor(currentTimestamp);
            
            if(tracking)
                opacityFactor = 1.0;
            
            if (opacityFactor <= 0)
                return;
            
            if(CalibrationHelper != null && CalibrationHelper.CalibratorType == CalibratorType.Plane)
                ComputeValues();
            
            Point pointO = transformer.Transform(points["o"]);
            Point pointA = transformer.Transform(points["a"]);
            Point pointB = transformer.Transform(points["b"]);
            Rectangle boundingBox = transformer.Transform(angleHelper.BoundingBox);

            using(Pen penEdges = styleHelper.GetBackgroundPen((int)(opacityFactor*255)))
            using(SolidBrush brushEdges = styleHelper.GetBackgroundBrush((int)(opacityFactor*255)))
            using(SolidBrush brushFill = styleHelper.GetBackgroundBrush((int)(opacityFactor*defaultBackgroundAlpha)))
            {
                // Disk section
                canvas.FillPie(brushFill, boundingBox, (float)angleHelper.Angle.Start, (float)angleHelper.Angle.Sweep);
                canvas.DrawPie(penEdges, boundingBox, (float)angleHelper.Angle.Start, (float)angleHelper.Angle.Sweep);
    
                // Edges
                canvas.DrawLine(penEdges, pointO, pointA);
                canvas.DrawLine(penEdges, pointO, pointB);
    
                // Handlers
                canvas.DrawEllipse(penEdges, pointO.Box(3));
                canvas.FillEllipse(brushEdges, pointA.Box(3));
                canvas.FillEllipse(brushEdges, pointB.Box(3));
                
                SolidBrush fontBrush = styleHelper.GetForegroundBrush((int)(opacityFactor * 255));
                int angle = (int)Math.Round(angleHelper.CalibratedAngle.Sweep);
                string label = angle.ToString() + "°";
                Font tempFont = styleHelper.GetFont((float)transformer.Scale);
                SizeF labelSize = canvas.MeasureString(label, tempFont);
                
                // Background
                float shiftx = (float)(transformer.Scale * angleHelper.TextPosition.X);
                float shifty = (float)(transformer.Scale * angleHelper.TextPosition.Y);
                PointF textOrigin = new PointF(shiftx + pointO.X - labelSize.Width / 2, shifty + pointO.Y - labelSize.Height / 2);
                RectangleF backRectangle = new RectangleF(textOrigin, labelSize);
                RoundedRectangle.Draw(canvas, backRectangle, brushFill, tempFont.Height/4, false, false, null);
        
                // Text
                canvas.DrawString(label, tempFont, fontBrush, backRectangle.Location);
                
                tempFont.Dispose();
                fontBrush.Dispose();
            }
        }
        public override int HitTest(Point point, long currentTimestamp, IImageToViewportTransformer transformer)
        {
            // Convention: miss = -1, object = 0, handle = n.
            int result = -1;
            int boxSide = transformer.Untransform(10);
            
            if (tracking || infosFading.GetOpacityFactor(currentTimestamp) > 0)
            {
                if (points["o"].Box(boxSide).Contains(point))
                    result = 1;
                else if (points["a"].Box(boxSide).Contains(point))
                    result = 2;
                else if (points["b"].Box(boxSide).Contains(point))
                    result = 3;
                else if (IsPointInObject(point))
                    result = 0;
            }
            
            return result;
        }
        public override void MoveHandle(Point point, int handle, Keys modifiers)
        {
            int constraintAngleSubdivisions = 8; // (Constraint by 45° steps).
            switch (handle)
            {
                case 1:
                    points["o"] = point;
                    SignalTrackablePointMoved("o");
                    break;
                case 2:
                    if((modifiers & Keys.Shift) == Keys.Shift)
                    {
                        PointF result = GeometryHelper.GetPointAtClosestRotationStepCardinal(points["o"], point, constraintAngleSubdivisions);
                        points["a"] = new Point((int)result.X, (int)result.Y);
                    }
                    else
                    {
                        points["a"] = point;
                    }
                    
                    SignalTrackablePointMoved("a");
                    break;
                case 3:
                    if((modifiers & Keys.Shift) == Keys.Shift)
                    {
                        PointF result = GeometryHelper.GetPointAtClosestRotationStepCardinal(points["o"], point, constraintAngleSubdivisions);
                        points["b"] = new Point((int)result.X, (int)result.Y);
                    }
                    else
                    {
                        points["b"] = point;
                    }
                    
                    SignalTrackablePointMoved("b");
                    break;
                default:
                    break;
            }

            ComputeValues();
        }
        public override void MoveDrawing(int deltaX, int deltaY, Keys modifierKeys)
        {
            points["o"] = new Point(points["o"].X + deltaX, points["o"].Y + deltaY);
            points["a"] = new Point(points["a"].X + deltaX, points["a"].Y + deltaY);
            points["b"] = new Point(points["b"].X + deltaX, points["b"].Y + deltaY);
            ComputeValues();
            SignalAllTrackablePointsMoved();
        }
        #endregion
            
        #region KVA Serialization
        private void ReadXml(XmlReader xmlReader, PointF scale)
        {
            xmlReader.ReadStartElement();
            
            while(xmlReader.NodeType == XmlNodeType.Element)
            {
                switch(xmlReader.Name)
                {
                    case "PointO":
                        points["o"] = XmlHelper.ParsePoint(xmlReader.ReadElementContentAsString());
                        break;
                    case "PointA":
                        points["a"] = XmlHelper.ParsePoint(xmlReader.ReadElementContentAsString());
                        break;
                    case "PointB":
                        points["b"] = XmlHelper.ParsePoint(xmlReader.ReadElementContentAsString());
                        break;
                    case "DrawingStyle":
                        style = new DrawingStyle(xmlReader);
                        BindStyle();
                        break;
                    case "InfosFading":
                        infosFading.ReadXml(xmlReader);
                        break;
                    default:
                        string unparsed = xmlReader.ReadOuterXml();
                        log.DebugFormat("Unparsed content in KVA XML: {0}", unparsed);
                        break;
                }
            }
            
            xmlReader.ReadEndElement();
            
            points["o"] = new Point((int)((float)points["o"].X * scale.X), (int)((float)points["o"].Y * scale.Y));
            points["a"] = new Point((int)((float)points["a"].X * scale.X), (int)((float)points["a"].Y * scale.Y));
            points["b"] = new Point((int)((float)points["b"].X * scale.X), (int)((float)points["b"].Y * scale.Y));

            ComputeValues();
        }
        public void WriteXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString("PointO", String.Format(CultureInfo.InvariantCulture, "{0};{1}", points["o"].X, points["o"].Y));
            xmlWriter.WriteElementString("PointA", String.Format(CultureInfo.InvariantCulture, "{0};{1}", points["a"].X, points["a"].Y));
            xmlWriter.WriteElementString("PointB", String.Format(CultureInfo.InvariantCulture, "{0};{1}", points["b"].X, points["b"].Y));
            
            xmlWriter.WriteStartElement("DrawingStyle");
            style.WriteXml(xmlWriter);
            xmlWriter.WriteEndElement();
            
            xmlWriter.WriteStartElement("InfosFading");
            infosFading.WriteXml(xmlWriter);
            xmlWriter.WriteEndElement();
            
            // Spreadsheet support.
            xmlWriter.WriteStartElement("Measure");        	
            int angle = (int)Math.Floor(-angleHelper.CalibratedAngle.Sweep);
            xmlWriter.WriteAttributeString("UserAngle", angle.ToString());
            xmlWriter.WriteEndElement();
        }
        #endregion
        
        #region IInitializable implementation
        public void ContinueSetup(Point point, Keys modifiers)
        {
            MoveHandle(point, 3, modifiers);
        }
        #endregion
        
        #region ITrackable implementation and support.
        public Guid ID
        {
            get { return id; }
        }
        public Dictionary<string, Point> GetTrackablePoints()
        {
            return points;
        }
        public void SetTracking(bool tracking)
        {
            this.tracking = tracking;
        }
        public void SetTrackablePointValue(string name, Point value)
        {
            if(!points.ContainsKey(name))
                throw new ArgumentException("This point is not bound.");
            
            points[name] = value;
            ComputeValues();
        }
        private void SignalAllTrackablePointsMoved()
        {
            if(TrackablePointMoved == null)
                return;
            
            foreach(KeyValuePair<string, Point> p in points)
                TrackablePointMoved(this, new TrackablePointMovedEventArgs(p.Key, p.Value));
        }
        private void SignalTrackablePointMoved(string name)
        {
            if(TrackablePointMoved == null || !points.ContainsKey(name))
                return;
            
            TrackablePointMoved(this, new TrackablePointMovedEventArgs(name, points[name]));
        }
        #endregion
        
        #region Specific context menu
        private void mnuInvertAngle_Click(object sender, EventArgs e)
        {
            Point temp = points["a"];
            points["a"] = points["b"];
            points["b"] = temp;
            ComputeValues();
            SignalAllTrackablePointsMoved();
            CallInvalidateFromMenu(sender);
        }
        #endregion
        
        #region Lower level helpers
        private void BindStyle()
        {
            style.Bind(styleHelper, "Bicolor", "line color");
        }
        private void ComputeValues()
        {
            FixIfNull();
            angleHelper.Update(points["o"], points["a"], points["b"], 0, Color.Transparent, CalibrationHelper);
        }
        private void FixIfNull()
        {
            if (points["a"] == points["o"])
                points["a"] = new Point(points["o"].X + 50, points["o"].Y);

            if (points["b"] == points["o"])
                points["b"] = new Point(points["o"].X, points["o"].Y - 50);
        }
        private bool IsPointInObject(Point _point)
        {
            return angleHelper.Hit(_point);
        }
        #endregion
    }

       
}