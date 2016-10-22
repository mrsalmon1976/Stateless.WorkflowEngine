
var DashboardView = function () {

    var that = this;

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadConnections(); });
        $('#btn-add-connection').on('click', function () { that.showForm(''); });
        $('#btn-submit-connection').on('click', that.submitForm);
        $('#btn-test-connection').on('click', that.testConnection);
        this.loadConnections();
    };

    this.confirmDeleteConnection = function (evt) {
        //debugger;
        var anchor = evt.currentTarget;
        var connId = anchor.attributes['data-model-id'].value;
        var dbName = anchor.attributes['data-model-db'].value;
        bootbox.confirm('Are you sure you want to delete this connection?', function (result) {
            if (result == true) {
                that.deleteConnection(connId);
            }
        });
    };

    this.deleteConnection = function (connId) {

        $('#pnl-loading').show();

        var request = $.ajax({
            url: "/connection/delete",
            method: "POST",
            dataType: 'html',
            data: { "id": connId }
        });

        request.done(function (response) {
            //debugger;
            that.loadConnections();
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            alert('error: ' + xhr.responseText);
        });
        request.always(function (xhr, textStatus) {
            $('#pnl-loading').hide();
        });
    };

    this.loadConnections = function () {

        $('#pnl-loading').show();
        $('#pnl-connections').html('');
        $('a.btn-delete').off('click');

        var request = $.ajax({
            url: "/connection/list",
            method: "GET",
            dataType: 'html'
        });

        request.done(function (response) {
            //debugger;
            $('#pnl-connections').html(response);
            $('a.btn-delete').on('click', that.confirmDeleteConnection);

        });

        request.fail(function (xhr, textStatus, errorThrown) {
            Utils.handleAjaxError(xhr, $('#pnl-connections'));
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

    this.testConnection = function () {
        $("#msg-error").addClass('hidden');
        var formData = $('#form-connection').serializeForm();
        var request = $.ajax({
            url: "/connection/test",
            method: "POST",
            data: formData,
            dataType: 'json',
            traditional: true
        });

        request.done(function (response) {
            //debugger;
            if (response.success) {
                bootbox.alert('Connection succeeded!');
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
