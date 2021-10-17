
var DashboardView = function () {

    var that = this;

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadConnections(); });
        $('#btn-add-connection').on('click', function () { that.showForm(''); });
        $('#btn-submit-connection').on('click', that.submitForm);
        $('#btn-test-connection').on('click', that.testConnection);
        this.loadConnections();
        this.checkForUpdates();
    };

    this.checkForUpdates = function () {

        var request = $.ajax({
            url: "/dashboard/checkforupdate",
            method: "GET",
            dataType: 'json'
        });

        request.done(function (response) {

            if (response.isNewVersionAvailable) {
                $('#span-version').html(response.latestReleaseVersionNumber);
                $("#pnl-version").show();
            }

        });
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

    this.getInfoPanelClass = function (num, warningThreshold, errorThreshold) {
        if (num === null || num === '' || parseInt(num) > errorThreshold) {
            return "conn-workflow-info-error";
        }
        if (parseInt(num) > warningThreshold) {
            return "conn-workflow-info-warning";
        }
        return '';
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
            that.loadConnectionInfo();

        });

        request.fail(function (xhr, textStatus, errorThrown) {
            Utils.handleAjaxError(xhr, $('#pnl-connections'));
        });
        request.always(function (xhr, textStatus) {
            //debugger;
            $('#pnl-loading').hide();
        });
    };

    this.loadConnectionInfo = function () {
        $(".panel-connection").each(function (index) {

            var connId = $(this).attr('data-model-id');
            var pnl = $(this);

            var pnlHeading = pnl.find('.panel-heading');

            // get the connection item panels and clear out any styles at the same time
            var pnlActive = pnl.find('.conn-row-active').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');
            var pnlSuspended = pnl.find('.conn-row-suspended').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');
            var pnlComplete = pnl.find('.conn-row-complete').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');
            var pnlTitle = pnl.find('.conn-title-link');

            var request = $.ajax({
                url: "/connection/info",
                method: "POST",
                dataType: 'json',
                data: { id : connId }
            });

            request.done(function (response) {

                // if an error has come back, add the error class and also add the error to the title
                if (response.connectionError != null && response.connectionError.length > 0) {
                    pnlHeading.addClass('error');
                    pnlActive.addClass('conn-workflow-info-error');
                    pnlSuspended.addClass('conn-workflow-info-error');
                    pnlComplete.addClass('conn-workflow-info-error');
                    pnlTitle.attr('title', response.connectionError);
                }
                else {
                    // the response is good - we can set the number and set any error/warning styles
                    pnlHeading.removeClass('error');
                    pnlActive.addClass(that.getInfoPanelClass(response.activeCount, 100, 10000));
                    pnlSuspended.addClass(that.getInfoPanelClass(response.suspendedCount, 0, 0));
                    pnlComplete.addClass(that.getInfoPanelClass(response.completeCount, Number.MAX_VALUE, Number.MAX_VALUE));
                    pnlActive.find('.conn-row-span-active').text(numeral(response.activeCount).format('0,0'));
                    pnlSuspended.find('.conn-row-span-suspended').text(numeral(response.suspendedCount).format('0,0'));
                    pnlComplete.find('.conn-row-span-complete').text(numeral(response.completeCount).format('0,0'));
                    pnlTitle.attr('title', pnlTitle.attr('data-title'));
                    pnl.find('.conn-row-span-data').show();
                }
            });

            request.fail(function (xhr, textStatus, errorThrown) {
                pnlHeading.addClass('error');
                pnlActive.addClass('conn-workflow-info-error');
                pnlSuspended.addClass('conn-workflow-info-error');
                pnlComplete.addClass('conn-workflow-info-error');
                pnlTitle.attr('title', xhr.responseText);
            });
            request.always(function (xhr, textStatus) {
                // always hide the loading image
                pnl.find('.conn-row-span-loading').hide();
            });
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
