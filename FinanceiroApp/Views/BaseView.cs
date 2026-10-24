using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public interface IAppView
{
    void RefreshData();
}

public abstract class BaseView : UserControl, IAppView
{
    protected readonly FinanceService Service;
    protected readonly Panel Content;

    protected BaseView(FinanceService service, string title)
    {
        Service = service;
        Dock = DockStyle.Fill;
        BackColor = Ui.Background;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(18, 12, 18, 6),
            BackColor = Ui.Background
        };
        header.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = Ui.TitleFont,
            ForeColor = Ui.Text
        });

        Content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 0, 18, 18),
            BackColor = Ui.Background
        };

        Controls.Add(Content);
        Controls.Add(header);
    }

    public abstract void RefreshData();

    protected void Run(Action action, Action? after = null)
    {
        try
        {
            action();
            after?.Invoke();
        }
        catch (Exception ex)
        {
            Ui.ShowError(ex);
        }
    }

    protected void RunSave(Action action, Action? after = null, string message = "Registro salvo com sucesso.")
    {
        try
        {
            action();
            after?.Invoke();
            Ui.ShowInfo(message);
        }
        catch (Exception ex)
        {
            Ui.ShowError(ex);
        }
    }

    protected void ShowEntryDialog(string title, Control fields, Action save, Action? after = null, string message = "Registro salvo com sucesso.")
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(430, 260),
            Size = new Size(520, 560),
            Font = Ui.BodyFont,
            BackColor = Ui.Background,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 0),
            Font = Ui.HeadingFont,
            ForeColor = Ui.Text,
            BackColor = Ui.Background
        };

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Ui.Surface,
            Padding = new Padding(12)
        };
        fields.Dock = DockStyle.Top;
        scroll.Controls.Add(fields);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Ui.Surface
        };

        var cancel = Ui.Button("Cancelar");
        cancel.Click += (_, _) => dialog.Close();
        var confirm = Ui.Button("Salvar", (_, _) =>
        {
            try
            {
                save();
                dialog.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                Ui.ShowError(ex);
            }
        }, true);

        actions.Controls.Add(confirm);
        actions.Controls.Add(cancel);

        dialog.Controls.Add(scroll);
        dialog.Controls.Add(actions);
        dialog.Controls.Add(titleLabel);
        dialog.AcceptButton = confirm;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Run(() =>
            {
                after?.Invoke();
                Ui.ShowInfo(message);
            });
        }
    }

    protected static Panel SurfacePanel(DockStyle dock = DockStyle.Fill)
    {
        return new Panel
        {
            Dock = dock,
            BackColor = Ui.Surface,
            Padding = new Padding(10)
        };
    }
}
