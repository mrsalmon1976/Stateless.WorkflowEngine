
var CustomDashboardView = function () {

    var that = this;

    this.errorSelector = '#dashboard-msg-error';

    this.init = function () {
        this.loadDashboards();

        this.initConnectionsMultiselect();

        $('#btn-add-dashboard').on('click', function () { that.showForm(); });
        $('#btn-submit-dashboard').on('click', that.submitForm);

        $('#dlg-dashboard').on('hidden.bs.modal', function () {
            that.resetForm();
        });
    };

    this.initConnectionsMultiselect = function () {
        try { $('#sel-connections').multiselect('destroy'); } catch (e) {}
        $('#sel-connections').multiselect({
            nonSelectedText: 'Select connections...',
            numberDisplayed: 2,
            buttonWidth: '100%'
        });
    };

    this.loadDashboards = function () {
        $('#pnl-loading').show();

        var request = $.ajax({
            url: "/customdashboard/list",
            method: "GET",
            dataType: 'html'
        });

        request.done(function (response) {
            $('#pnl-dashboards').html(response);
            $('a.btn-delete-dashboard').on('click', that.confirmDelete);
            $('a.btn-edit-dashboard').on('click', that.showEditForm);
        });

        request.fail(function (xhr) {
            Utils.handleAjaxError(xhr, $('#pnl-dashboards'));
        });

        request.always(function () {
            $('#pnl-loading').hide();
        });
    };

    this.confirmDelete = function (evt) {
        evt.preventDefault();
        var anchor = evt.currentTarget;
        var id = anchor.attributes['data-model-id'].value;
        var name = anchor.attributes['data-model-name'].value;
        bootbox.confirm('Are you sure you want to delete the dashboard "' + name + '"?', function (result) {
            if (result) {
                that.deleteDashboard(id);
            }
        });
    };

    this.deleteDashboard = function (id) {
        $('#pnl-loading').show();

        var request = $.ajax({
            url: "/customdashboard/delete",
            method: "POST",
            dataType: 'json',
            data: { id: id }
        });

        request.done(function (response) {
            if (response.success) {
                that.loadDashboards();
            } else {
                bootbox.alert('Error: ' + (response.messages ? response.messages[0] : 'Unknown error'));
            }
        });

        request.fail(function (xhr) {
            if (Utils.isAuthError(xhr)) { return; }
            bootbox.alert('A fatal error occurred');
        });

        request.always(function () {
            $('#pnl-loading').hide();
        });
    };

    this.showForm = function () {
        Utils.hideError(that.errorSelector);
        $('#dlg-dashboard-title').text('Add dashboard');
        $('#dlg-dashboard').modal('show');
    };

    this.showEditForm = function (evt) {
        evt.preventDefault();
        var anchor = evt.currentTarget;
        var id = anchor.getAttribute('data-model-id');
        var name = anchor.getAttribute('data-model-name');
        var connectionIds = anchor.getAttribute('data-connection-ids');

        Utils.hideError(that.errorSelector);
        $('#dlg-dashboard-title').text('Edit dashboard');
        $('#hid-dashboard-id').val(id);
        $('#txt-dashboard-name').val(name);

        var ids = connectionIds && connectionIds.trim() !== ''
            ? connectionIds.split(',').filter(function (s) { return s.trim() !== ''; })
            : [];
        $('#sel-connections option').each(function () {
            $(this).prop('selected', ids.indexOf($(this).val()) >= 0);
        });
        that.initConnectionsMultiselect();

        $('#dlg-dashboard').modal('show');
    };

    this.resetForm = function () {
        $('#form-dashboard')[0].reset();
        $('#hid-dashboard-id').val('');
        $('#dlg-dashboard-title').text('Add dashboard');
        $('#sel-connections option').prop('selected', false);
        that.initConnectionsMultiselect();
        Utils.hideError(that.errorSelector);
    };

    this.submitForm = function () {
        Utils.hideError(that.errorSelector);

        var name = $('#txt-dashboard-name').val();
        if (!name || name.trim() === '') {
            Utils.showError(that.errorSelector, 'Dashboard name is required');
            return;
        }

        var selectedIds = $('#sel-connections').val() || [];
        var formData = {
            id: $('#hid-dashboard-id').val(),
            name: name,
            connectionIds: selectedIds.join(',')
        };

        var request = $.ajax({
            url: "/customdashboard/save",
            method: "POST",
            data: formData,
            dataType: 'json'
        });

        request.done(function (response) {
            if (response.success) {
                $('#dlg-dashboard').modal('hide');
                that.loadDashboards();
            } else {
                Utils.showError(that.errorSelector, response.messages ? response.messages[0] : 'Save failed');
            }
        });

        request.fail(function (xhr) {
            if (Utils.isAuthError(xhr)) { return; }
            Utils.showError(that.errorSelector, 'A fatal error occurred');
        });
    };

};


$(document).ready(function () {
    new CustomDashboardView().init();
});
