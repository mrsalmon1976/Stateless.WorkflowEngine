
var DashboardView = function () {

    var that = this;

    this.init = function () {
        $('#btn-add-connection').on('click', function () { that.showForm(''); });
        $('#btn-submit-connection').on('click', that.submitForm);
        this.loadConnections();
    };

    this.loadConnections = function () {

        $('#pnl-loading').show();

        var request = $.ajax({
            url: "/connection/list",
            method: "GET",
            dataType: 'html'
        });

        request.done(function (response) {
            //debugger;
            $('#pnl-connections').html(response);
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            alert('error: ' + xhr.responseText);
        });
        request.always(function (xhr, textStatus) {
            //debugger;
            $('#pnl-loading').hide();
        });
    };

    this.showError = function (error) {
        //debugger;
        var err = error;
        if ($.isArray(err)) {
            err = Collections.displayList(err);
        }
        $("#connection-msg-error").html(err);
        $("#connection-msg-error").removeClass('hidden');
    };

    this.showForm = function (connectionId) {
        $('#hid-connection-id').val(connectionId);
        $('#btn-submit-connection').text(connectionId == '' ? 'Add connection' : 'Update connection');
        $("#connection-msg-error").addClass('hidden');
        $('#dlg-connection').modal('show');
    };

    this.submitForm = function () {
        $("#msg-error").addClass('hidden');
        var formData = $('#form-connection').serializeForm();
        var request = $.ajax({
            url: "/connection/save",
            method: "POST",
            data: formData,
            dataType: 'json',
            traditional: true
        });

        request.done(function (response) {
            //debugger;
            if (response.success) {
                $('#dlg-connection').modal('hide');
                that.loadConnections();
            }
            else {
                that.showError(response.messages);
            }
        });

        request.fail(function (xhr, textStatus) {
            try {
                that.showError(xhr.responseJSON.message);
            }
            catch (err) {
                that.showError('A fatal error occurred');
            }
        });
    };

}


$(document).ready(function()
{
    new DashboardView().init();
});
