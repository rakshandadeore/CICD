<%@ Page Language="C#" MasterPageFile="~/MasterPages/FormDetail.master" AutoEventWireup="true" ValidateRequest="false" CodeFile="ZW101000.aspx.cs" Inherits="Page_ZW101000" Title="Untitled Page" %>

<%@ MasterType VirtualPath="~/MasterPages/FormDetail.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" runat="Server">
    <px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
        TypeName="ZWJCLEnergyExtendedFunctionality.ZWJCLEnergyExtendedFunctionality.Graph.ZWPortsOfEntryGraphEntry"
        PrimaryView="Ports">
        <CallbackCommands>
        </CallbackCommands>
    </px:PXDataSource>
</asp:Content>
<asp:Content ID="cont2" ContentPlaceHolderID="phF" runat="Server">
    <px:PXFormView ID="form" runat="server" DataSourceID="ds" DataMember="Ports" AllowAutoHide="false" Height="606px" Width="100%">
        <Template>
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="M" ControlSize="M" />
            <px:PXSelector CommitChanges="True" ID="edPortID" runat="server" DataField="PortID" AllowEdit="True" AutoRefresh="true" />
            <px:PXTextEdit CommitChanges="True" ID="edDescription" runat="server" DataField="Description" AllowEdit="True" />
            <px:PXNumberEdit CommitChanges="True" ID="edOceanDays" runat="server" DataField="OceanDays" AllowEdit="True" />
            <px:PXNumberEdit CommitChanges="True" ID="edInLandDays" runat="server" DataField="InLandDays" AllowEdit="True" />
             <px:PXNumberEdit CommitChanges="True" ID="edTotalDays" runat="server" DataField="TotalDays" AllowEdit="True" />
            <px:PXCheckBox CommitChanges="True" ID="edActive" runat="server" DataField="Active" AllowEdit="True" />
        </Template>
    </px:PXFormView>
</asp:Content>

