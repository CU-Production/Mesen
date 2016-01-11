﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Mesen.GUI.Forms;

namespace Mesen.GUI.Debugger
{
	public partial class frmDebugger : BaseForm
	{
		private List<Form> _childForms = new List<Form>();
		private InteropEmu.NotificationListener _notifListener;
		private ctrlDebuggerCode _lastCodeWindow;

		public frmDebugger()
		{
			InitializeComponent();

			_lastCodeWindow = ctrlDebuggerCode;

			BreakpointManager.Breakpoints.Clear();
			BreakpointManager.BreakpointsChanged += BreakpointManager_BreakpointsChanged;
		}

		protected override void OnLoad(EventArgs e)
		{
 			base.OnLoad(e);

			_notifListener = new InteropEmu.NotificationListener();
			_notifListener.OnNotification += _notifListener_OnNotification;

			InteropEmu.DebugInitialize();
			
			//Pause a few frames later to give the debugger a chance to disassemble some code
			InteropEmu.DebugStep(100000);

			UpdateCdlRatios();
			tmrCdlRatios.Start();
		}

		private void UpdateCdlRatios()
		{
			CdlRatios ratios = new CdlRatios();
			InteropEmu.DebugGetCdlRatios(ref ratios);

			lblPrgAnalysisResult.Text = string.Format("{0:0.00}% (Code: {1:0.00}%, Data: {2:0.00}%, Unknown: {3:0.00}%)", ratios.PrgRatio * 100, ratios.CodeRatio * 100, ratios.DataRatio * 100, (1 - ratios.PrgRatio) * 100);
			if(ratios.ChrRatio >= 0) {
				lblChrAnalysisResult.Text = string.Format("{0:0.00}% (Drawn: {1:0.00}%, Read: {2:0.00}%, Unknown: {3:0.00}%)", ratios.ChrRatio * 100, ratios.ChrDrawnRatio * 100, ratios.ChrReadRatio * 100, (1 - ratios.ChrRatio) * 100);
			} else {
				lblChrAnalysisResult.Text = "N/A (CHR RAM)";
			}
		}

		private void _notifListener_OnNotification(InteropEmu.NotificationEventArgs e)
		{
			if(e.NotificationType == InteropEmu.ConsoleNotificationType.CodeBreak) {
				this.BeginInvoke((MethodInvoker)(() => UpdateDebugger()));
			}
		}

		private bool UpdateSplitView()
		{
			if(mnuSplitView.Checked) {
				tlpTop.ColumnStyles[1].SizeType = SizeType.Percent;
				tlpTop.ColumnStyles[0].Width = 50f;
				tlpTop.ColumnStyles[1].Width = 50f;
				this.MinimumSize = new Size(1250, 650);
			} else {
				tlpTop.ColumnStyles[1].SizeType = SizeType.Absolute;
				tlpTop.ColumnStyles[1].Width = 0f;
				this.MinimumSize = new Size(1000, 650);
			}
			ctrlDebuggerCodeSplit.Visible = mnuSplitView.Checked;
			return mnuSplitView.Checked;
		}

		private void UpdateDebugger()
		{
			if(InteropEmu.DebugIsCodeChanged()) {
				string code = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(InteropEmu.DebugGetCode());
				ctrlDebuggerCode.Code = code;
				ctrlDebuggerCodeSplit.Code = code;
			}

			DebugState state = new DebugState();
			InteropEmu.DebugGetState(ref state);

			if(UpdateSplitView()) {
				ctrlDebuggerCodeSplit.UpdateCode(true);
			} else {
				_lastCodeWindow = ctrlDebuggerCode;
			}

			ctrlDebuggerCode.SelectActiveAddress(state.CPU.DebugPC);
			ctrlDebuggerCodeSplit.SetActiveAddress(state.CPU.DebugPC);
			RefreshBreakpoints();

			ctrlConsoleStatus.UpdateStatus(ref state);
			ctrlWatch.UpdateWatch();
			ctrlCallstack.UpdateCallstack();

			this.BringToFront();
		}

		private void ClearActiveStatement()
		{
			ctrlDebuggerCode.ClearActiveAddress();
			ctrlDebuggerCodeSplit.ClearActiveAddress();
			RefreshBreakpoints();
		}

		private void ToggleBreakpoint(bool toggleEnabled)
		{
			BreakpointManager.ToggleBreakpoint(_lastCodeWindow.GetCurrentLine(), toggleEnabled);
		}
		
		private void RefreshBreakpoints()
		{
			ctrlDebuggerCodeSplit.HighlightBreakpoints();
			ctrlDebuggerCode.HighlightBreakpoints();
		}

		private void OpenChildForm(Form frm)
		{
			this._childForms.Add(frm);
			frm.FormClosed += (obj, args) => {
				this._childForms.Remove((Form)obj);
			};
			frm.Show();
		}

		private void mnuContinue_Click(object sender, EventArgs e)
		{
			ClearActiveStatement();
			InteropEmu.DebugRun();
		}

		private void frmDebugger_FormClosed(object sender, FormClosedEventArgs e)
		{
			InteropEmu.DebugRelease();
		}

		private void mnuToggleBreakpoint_Click(object sender, EventArgs e)
		{
			ToggleBreakpoint(false);
		}

		private void mnuDisableEnableBreakpoint_Click(object sender, EventArgs e)
		{
			ToggleBreakpoint(true);
		}

		private void mnuBreak_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugStep(1);
		}

		private void mnuStepInto_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugStep(1);
		}

		private void mnuStepOut_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugStepOut();
		}
		
		private void mnuStepOver_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugStepOver();
		}

		private void mnuRunOneFrame_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugStepCycles(29780);
		}

		private void ctrlDebuggerCode_OnWatchAdded(AddressEventArgs args)
		{
			this.ctrlWatch.AddWatch(args.Address);
		}

		private void ctrlDebuggerCode_OnSetNextStatement(AddressEventArgs args)
		{
			UInt16 addr = (UInt16)args.Address;
			InteropEmu.DebugSetNextStatement(addr);
			this.UpdateDebugger();
		}

		private void mnuFind_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.OpenSearchBox();
		}
		
		private void mnuFindNext_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.FindNext();
		}

		private void mnuFindPrev_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.FindPrevious();
		}

		private void mnuSplitView_Click(object sender, EventArgs e)
		{
			UpdateDebugger();
		}

		private void mnuMemoryViewer_Click(object sender, EventArgs e)
		{
			OpenChildForm(new frmMemoryViewer());
		}

		private void BreakpointManager_BreakpointsChanged(object sender, EventArgs e)
		{
			RefreshBreakpoints();
		}

		private void ctrlDebuggerCode_Enter(object sender, EventArgs e)
		{
			_lastCodeWindow = ctrlDebuggerCode;
		}

		private void ctrlDebuggerCodeSplit_Enter(object sender, EventArgs e)
		{
			_lastCodeWindow = ctrlDebuggerCodeSplit;
		}

		private void mnuGoToAddress_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.GoToAddress();
		}

		private void mnuGoToIrqHandler_Click(object sender, EventArgs e)
		{
			int address = (InteropEmu.DebugGetMemoryValue(0xFFFF) << 8) | InteropEmu.DebugGetMemoryValue(0xFFFE);
			_lastCodeWindow.ScrollToLineNumber(address);
		}

		private void mnuGoToNmiHandler_Click(object sender, EventArgs e)
		{
			int address = (InteropEmu.DebugGetMemoryValue(0xFFFB) << 8) | InteropEmu.DebugGetMemoryValue(0xFFFA);
			_lastCodeWindow.ScrollToLineNumber(address);
		}

		private void mnuGoToResetHandler_Click(object sender, EventArgs e)
		{
			int address = (InteropEmu.DebugGetMemoryValue(0xFFFD) << 8) | InteropEmu.DebugGetMemoryValue(0xFFFC);
			_lastCodeWindow.ScrollToLineNumber(address);
		}

		private void mnuIncreaseFontSize_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.FontSize++;
		}

		private void mnuDecreaseFontSize_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.FontSize--;
		}

		private void mnuResetFontSize_Click(object sender, EventArgs e)
		{
			_lastCodeWindow.FontSize = 13;
		}

		private void mnuClose_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			foreach(Form frm in this._childForms.ToArray()) {
				frm.Close();
			}
			base.OnFormClosed(e);
		}

		private void mnuNametableViewer_Click(object sender, EventArgs e)
		{
			OpenChildForm(new frmPpuViewer());
		}

		private void ctrlCallstack_FunctionSelected(object sender, EventArgs e)
		{
			_lastCodeWindow.ScrollToLineNumber((int)sender);
		}

		private void tmrCdlRatios_Tick(object sender, EventArgs e)
		{
			this.UpdateCdlRatios();
		}

		private void mnuLoadCdlFile_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "CDL files (*.cdl)|*.cdl";
			if(ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				if(!InteropEmu.DebugLoadCdlFile(ofd.FileName)) {
					MessageBox.Show("Could not load CDL file.  The file selected file is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void mnuSaveAsCdlFile_Click(object sender, EventArgs e)
		{
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.Filter = "CDL files (*.cdl)|*.cdl";
			sfd.AddExtension = true;
			if(sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				if(!InteropEmu.DebugSaveCdlFile(sfd.FileName)) {
					MessageBox.Show("Error while trying to save CDL file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void mnuResetCdlLog_Click(object sender, EventArgs e)
		{
			InteropEmu.DebugResetCdlLog();
		}

		private void ctrlBreakpoints_BreakpointNavigation(object sender, EventArgs e)
		{
			_lastCodeWindow.ScrollToLineNumber((int)((Breakpoint)sender).Address);
		}

		private void mnuTraceLogger_Click(object sender, EventArgs e)
		{
			new frmTraceLogger().Show();
		}
	}
}
