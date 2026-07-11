namespace KKL.WordStudio.UI.ViewModels;

/// <summary>Physical width state of the right Context Dock. Pure UI/ViewModel state — deliberately not in Domain (see Variant 2.5 task instructions).</summary>
public enum DockState { Normal, Collapsed, Expanded }

/// <summary>Which content the Context Dock is currently showing. ChangeBinding is a navigation state reached from Properties, not a top-level tab.</summary>
public enum DockPage { Contents, Properties, Warnings, ChangeBinding }
