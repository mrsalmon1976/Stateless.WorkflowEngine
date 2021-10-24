
var DashboardView = function () {

    var that = this;

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadConnections(); });
        $('#btn-add-connection').on('click', function () { that.showForm(''); });
        $('#btn-layout-list').on('click', function () { that.toggleLayout('list'); });
        $('#btn-layout-panels').on('click', function () { that.toggleLayout('panels'); });
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
            that.toggleLayout(localStorage.dashboardLayout);
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

        $(".connection-wrapper").each(function (index) {

            var connId = $(this).attr('data-model-id');

            var connectionWrapper = $(this);
            connectionWrapper.removeClass('conn-workflow-info-error');
            //var panelHeading = connectionWrapper.find('.panel-heading');

            //// get the connection item panels and clear out any styles at the same time
            //var pnlActive = connectionWrapper.find('.conn-row-active').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');
            //var pnlSuspended = connectionWrapper.find('.conn-row-suspended').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');
            //var pnlComplete = connectionWrapper.find('.conn-row-complete').removeClass('conn-workflow-info-error').removeClass('conn-workflow-info-warning');

            var request = $.ajax({
                url: "/connection/info",
                method: "POST",
                dataType: 'json',
                data: { id : connId }
            });

            request.done(function (response) {
                that.renderConnection(connectionWrapper, response.connectionError, response);
            });

            request.fail(function (xhr, textStatus, errorThrown) {
                that.renderConnection(connectionWrapper, xhr.responseText);
            });
            request.always(function (xhr, textStatus) {
                // always hide the loading image
                connectionWrapper.find('.conn-row-span-loading').hide();
            });
         });
    };

    this.renderConnection = function (connectionWrapper, connectionError, response) {

        var activeCountText = 'Error';
        var suspendedCountText = 'Error';
        var completeCountText = 'Error';
        var connectionWrapperTitle = '';

        var dataElement = connectionWrapper.find('.conn-row-span-data');
        var activeElement = connectionWrapper.find('.conn-row-active').removeClass('conn-workflow-info-error');
        var suspendedElement = connectionWrapper.find('.conn-row-suspended').removeClass('conn-workflow-info-error');
        var completeElement = connectionWrapper.find('.conn-row-complete').removeClass('conn-workflow-info-error');
        var panelHeadingElement = connectionWrapper.find('.panel-heading').removeClass('error');
        var tableCells = connectionWrapper.find('td').removeClass('conn-workflow-info-error');
        var divs = connectionWrapper.find('div').removeClass('conn-workflow-info-error');

        // if an error has come back, add the error class and also add the error to the title
        if (connectionError !== null && connectionError.length > 0) {
            connectionWrapper.addClass('conn-workflow-info-error');
            panelHeadingElement.addClass('error');
            activeElement.addClass('conn-workflow-info-error');
            suspendedElement.addClass('conn-workflow-info-error');
            completeElement.addClass('conn-workflow-info-error');
            tableCells.addClass('conn-workflow-info-error');
            divs.addClass('conn-workflow-info-error');
            //    pnlSuspended.addClass('conn-workflow-info-error');
            //    pnlComplete.addClass('conn-workflow-info-error');
            connectionWrapperTitle = connectionError;
        }
        else {
            connectionWrapper.removeClass('conn-workflow-info-error');
            activeElement.addClass(that.getInfoPanelClass(response.activeCount, 100, 10000));
            suspendedElement.addClass(that.getInfoPanelClass(response.suspendedCount, 0, 0));
            activeCountText = numeral(response.activeCount).format('0,0');
            suspendedCountText = numeral(response.suspendedCount).format('0,0');
            completeCountText = numeral(response.completeCount).format('0,0');
            //    pnlSuspended.find('.conn-row-span-suspended').text(numeral(response.suspendedCount).format('0,0'));
            //    pnlComplete.find('.conn-row-span-complete').text(numeral(response.completeCount).format('0,0'));
        }

        activeElement.find('.conn-row-span-active').text(activeCountText);
        suspendedElement.find('.conn-row-span-suspended').text(suspendedCountText);
        completeElement.find('.conn-row-span-complete').text(completeCountText);
        connectionWrapper.attr('title', connectionWrapperTitle);
        dataElement.show();
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

    this.toggleLayout = function (dashboardLayout) {
        if (dashboardLayout === 'list') {
            $('#pnl-connections-panels').hide();
            $('#tbl-connections').show();
            $('#btn-layout-list').addClass('active');
            $('#btn-layout-panels').removeClass('active');

        }
        else {
            $('#tbl-connections').hide();
            $('#pnl-connections-panels').show();
            $('#btn-layout-list').removeClass('active');
            $('#btn-layout-panels').addClass('active');
        }
        localStorage.dashboardLayout = dashboardLayout;
    }

}


$(document).ready(function()
{
    new DashboardView().init();
});
