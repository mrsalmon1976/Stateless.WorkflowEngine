
var StoreView = function () {

    var that = this;

    this.workflowViewModel = null;

    this.getSelectedWorkflowIds = function () {
        var selectedBoxes = $('.chk-workflow:checked');
        var workflowIds = [];

        selectedBoxes.each(function () {
            var workflowId = $(this).val();
            workflowIds.push(workflowId);
        });

        return workflowIds;
    };

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadWorkflows(); });
        $('#btn-suspend').on('click', function () { that.workflowViewModel.toggleWorkflowSuspension(); });
        this.loadWorkflows();
    };

    this.loadWorkflows = function () {

        var connId = $('#pnl-workflows').data().modelId;
        $('#pnl-loading').show();
        $('#pnl-workflows').html('');

        // remove event handlers
        $('a.workflow-id').off('click');
        $('#chk-workflow-all').off('click');
        $('#btn-suspend').off('click');
        $('#btn-unsuspend').off('click');

        var request = $.ajax({
            url: "/store/list",
            method: "POST",
            dataType: 'html',
            data: { "id": connId }
        });

        request.done(function (response) {
            $('#pnl-workflows').html(response);
            // attach event handlers again
            $('a.workflow-id').on('click', function () { that.openWorkflowDialog($(this)); });
            $('#chk-workflow-all').on('click', function () { that.toggleWorkflowCheckboxes($(this)); });
            $('#btn-suspend').on('click', function () { that.suspendWorkflows(); });
            $('#btn-unsuspend').on('click', function () { that.unsuspendWorkflows(); });
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            Utils.handleAjaxError(xhr, $('#pnl-workflows'));

        });
        request.always(function (xhr, textStatus) {
            $('#pnl-loading').hide();
        });
    };

    this.openWorkflowDialog = function (evtSource) {

        if (that.workflowViewModel == null) {
            that.workflowViewModel = new StoreViewWorkflowViewModel();
        }
        that.workflowViewModel.showDialog(evtSource.data('id'), evtSource.data().type, function () { that.loadWorkflows(); });
    };

    this.submitWorkflowActions = function (workflowIds, target) {
        
        var model = {
            "WorkflowIds": workflowIds,
            "ConnectionId": $('#pnl-workflows').data().modelId
        };

        var request = $.ajax({
            url: target,
            method: "POST",
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            data: JSON.stringify(model)
        });
        request.done(function (response) {
            that.loadWorkflows();
        });
    };

    this.suspendWorkflows = function () {
        var workflowIds = this.getSelectedWorkflowIds();
        if (workflowIds.length == 0) {
            return;
        }
        bootbox.confirm({
            message: "Are you sure you want to suspend all selected workflows?",
            buttons: {
                confirm: {
                    label: ' Yes ',
                    className: 'btn-danger'
                },
                cancel: {
                    label: ' No ',
                    className: 'btn-default'
                }
            },
            callback: function (result) {
                if (result === true) {
                    that.submitWorkflowActions(workflowIds, '/store/suspend');
                }
            }
        });
    };

    this.toggleWorkflowCheckboxes = function (evtSource) {
        var checked = evtSource.is(':checked');
        $(".chk-workflow").each(function () {
            $(this).prop("checked", checked);
        });
    };

    this.unsuspendWorkflows = function () {
        var workflowIds = this.getSelectedWorkflowIds();
        if (workflowIds.length == 0) {
            return;
        }
        bootbox.confirm({
            message: "Are you sure you want to unsuspend all selected workflows?",
            buttons: {
                confirm: {
                    label: ' Yes ',
                    className: 'btn-danger'
                },
                cancel: {
                    label: ' No ',
                    className: 'btn-default'
                }
            },
            callback: function (result) {
                if (result === true) {
                    that.submitWorkflowActions(workflowIds, '/store/unsuspend');
                }
            }
        });
    };

};

var StoreViewWorkflowViewModel = function () {

    var that = this;

    this.workflowId = null;
    this.connectionId = null;
    this.closeDialogCallback = null;

    this.showDialog = function (workflowId, workflowTypeName, closeDialogCallback) {

        that.workflowId = workflowId;
        that.connectionId = $('#pnl-workflows').data().modelId;
        that.closeDialogCallback = closeDialogCallback;
        $('#txt-workflow-json').val('').show();
        $('#dlg-workflow').modal('show');
        $('#workflow-spinner').show();
        $('#workflow-msg-error').hide();
        $('#btn-suspend').prop('disabled', true);

        var request = $.ajax({
            url: "/store/workflow",
            method: "POST",
            dataType: 'json',
            data: { "WorkflowId": that.workflowId, "ConnectionId": that.connectionId }
        });

        request.done(function (response) {
            //debugger;
            $('#txt-workflow-json').val(response.workflowJson);
            $('#workflow-header-id').html(workflowTypeName + ' :: ' + that.workflowId);

            // pull stats we need from the json
            $('#btn-suspend').html(response.isSuspended ? 'Unsuspend' : 'Suspend');
            $('#btn-suspend').prop('disabled', false);
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            //debugger;
            if (xhr.status == 404 && xhr.responseJSON != null && xhr.responseJSON.message != null) {
                $('#workflow-msg-error').html(xhr.responseJSON.message);
                $('#workflow-header-id').html('Workflow Not Found');
            }
            else {
                $('#workflow-msg-error').html('An error has occurred: ' + xhr.errorThrown);
            }
            $('#txt-workflow-json').hide();
            $('#workflow-msg-error').show().removeClass('hidden');
        });
        request.always(function (xhr, textStatus) {
            $('#workflow-spinner').hide();
        });
    };

    this.toggleWorkflowSuspension = function () {
        //debugger;
        var suspend = ($('#btn-suspend').html() == 'Suspend');
        $('#spinner-suspend').show();
        $('#btn-suspend').prop('disabled', true);

        var model = {
            "WorkflowIds": [that.workflowId],
            "ConnectionId": that.connectionId
        };

        var request = $.ajax({
            url: (suspend ? "/store/suspend" : '/store/unsuspend'),
            method: "POST",
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            data: JSON.stringify(model)
        });

        request.done(function (response) {
            $('#dlg-workflow').modal('hide');
            if (that.closeDialogCallback != null) {
                that.closeDialogCallback();
                that.closeDialogCallback = null;
            }
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            bootbox.alert({ message: "Failed to suspend workflow: " + errorThrown, size: 'small' });
        });
        request.always(function (xhr, textStatus) {
            $('#spinner-suspend').hide();
            $('#btn-suspend').prop('disabled', false);
        });
    };

};

$(document).ready(function()
{
    new StoreView().init();
});
