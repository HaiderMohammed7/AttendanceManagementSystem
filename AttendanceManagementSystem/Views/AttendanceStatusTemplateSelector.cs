using System.Windows;
using System.Windows.Controls;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.ViewModels;

namespace AttendanceManagementSystem.Views;

public sealed class AttendanceStatusTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PresentTemplate { get; set; }
    public DataTemplate? AbsentTemplate { get; set; }
    public DataTemplate? NotMarkedTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item is AttendanceRowViewModel row
            ? row.Status switch
            {
                AttendanceStatus.Present => PresentTemplate,
                AttendanceStatus.Absent => AbsentTemplate,
                _ => NotMarkedTemplate
            }
            : NotMarkedTemplate;
    }
}