
var StoreView = function () {

    var that = this;

    this.workflowViewModel = null;
    this.workflowDefinitionViewModel = null;
    this.workflowCount = 50;

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
        $('#btn-suspend-single').on('click', function () { that.workflowViewModel.toggleWorkflowSuspension(); });
        $('#btn-delete-single').on('click', function () { that.workflowViewModel.confirmWorkflowDelete(); });
        $('#btn-count').html(that.workflowCount).on('click', function () { that.onWorkflowToolbarButtonClick(); });
        $('#btn-workflow-count-update').on('click', function () { that.onWorkflowCountUpdateButtonClick(); });
        this.loadWorkflows();
    };

    this.deleteWorkflows = function () {
        var workflowIds = this.getSelectedWorkflowIds();
        if (workflowIds.length == 0) {
            return;
        }
        bootbox.confirm({
            message: "Are you sure you want to delete all selected workflows?  Workflows will be deleted permanently.",
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
                    that.submitWorkflowActions(workflowIds, '/store/remove');
                }
            }
        });
    };

    this.loadWorkflows = function () {

        var connId = $('#pnl-workflows').data().modelId;
        $('#pnl-loading').show();
        $('#pnl-workflows').html('');

        // remove event handlers
        $('a.workflow-id').off('click');
        $('a.workflow-qualified-name').off('click');
        $('#chk-workflow-all').off('click');
        $('#btn-suspend').off('click');
        $('#btn-unsuspend').off('click');
        $('#btn-delete').off('click');

        var request = $.ajax({
            url: "/store/list",
            method: "POST",
            dataType: 'html',
            data: { "ConnectionId": connId, "WorkflowCount": that.workflowCount }
        });

        request.done(function (response) {
            $('#pnl-workflows').html(response);
            // attach event handlers again
            $('a.workflow-id').on('click', function () { that.openWorkflowDialog($(this)); });
            $('a.workflow-qualified-name').on('click', function () { that.openWorkflowDefinitionDialog($(this)); });
            $('#chk-workflow-all').on('click', function () { that.toggleWorkflowCheckboxes($(this)); });
            $('#btn-suspend').on('click', function () { that.suspendWorkflows(); });
            $('#btn-unsuspend').on('click', function () { that.unsuspendWorkflows(); });
            $('#btn-delete').on('click', function () { that.deleteWorkflows(); });
            $('#txt-workflow-count').on('keyup', function () { that.validateWorkflowCountDialog(); });
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            Utils.handleAjaxError(xhr, $('#pnl-workflows'));

        });
        request.always(function (xhr, textStatus) {
            $('#pnl-loading').hide();
        });
    };

    this.onWorkflowCountUpdateButtonClick = function () {
        that.workflowCount = $('#txt-workflow-count').val();
        $('#btn-count').html(that.workflowCount);
        $('#dlg-workflow-count').modal('hide')
        that.loadWorkflows();
    };

    this.onWorkflowToolbarButtonClick = function () {
        $('#txt-workflow-count').val(that.workflowCount);
        $('#dlg-workflow-count').modal('show').on('shown.bs.modal', function () {
            $('#txt-workflow-count').focus().select();
        });
    };


    this.openWorkflowDialog = function (evtSource) {

        if (that.workflowViewModel == null) {
            that.workflowViewModel = new StoreViewWorkflowViewModel();
        }
        that.workflowViewModel.showDialog(evtSource.data('id'), evtSource.data().type, function () { that.loadWorkflows(); });
    };

    this.openWorkflowDefinitionDialog = function (evtSource) {

        if (that.workflowDefinitionViewModel == null) {
            that.workflowDefinitionViewModel = new StoreViewWorkflowDefinitionViewModel();
        }
        that.workflowDefinitionViewModel.showDialog(evtSource.data('qualified-name'));
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

    this.validateWorkflowCountDialog = function () {
        var count = $('#txt-workflow-count').val();
        var isValid = !isNaN(count)
            && parseInt(Number(count)) == count
            && !isNaN(parseInt(count, 10))
            && (parseInt(count) > 0);
        $('#btn-workflow-count-update').prop('disabled', !isValid);
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

        var workflowJsonElement = $('#txt-workflow-json').val('').show();
        $('#dlg-workflow').modal('show');
        $('#spinner-single').show();
        $('#workflow-msg-error').hide();
        $('#btn-suspend-single').prop('disabled', true);
        $('#btn-delete-single').prop('disabled', true);

        var request = $.ajax({
            url: "/store/workflow",
            method: "POST",
            dataType: 'json',
            data: { "WorkflowId": that.workflowId, "ConnectionId": that.connectionId }
        });

        request.done(function (response) {
            //debugger;
            workflowJsonElement.val(response.workflowJson);
            $('#workflow-header-id').html(workflowTypeName + ' :: ' + that.workflowId);

            // pull stats we need from the json
            $('#btn-suspend-single').html(response.isSuspended ? 'Unsuspend' : 'Suspend');
            $('#btn-suspend-single').prop('disabled', false);
            $('#btn-delete-single').prop('disabled', false);
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
            workflowJsonElement.hide();
            $('#workflow-msg-error').show().removeClass('hidden');
        });
        request.always(function (xhr, textStatus) {
            $('#spinner-single').hide();
        });
    };

    this.confirmWorkflowDelete = function () {
        bootbox.confirm({
            message: "Are you sure you want to delete all this workflow?  The workflow will be permanently deleted.",
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
                    that.deleteWorkflow();
                }
            }
        });
    };

    this.deleteWorkflow = function () {
        $('#spinner-single').show();
        $('#btn-suspend-single').prop('disabled', true);
        $('#btn-delete-single').prop('disabled', true);

        var model = {
            "WorkflowIds": [that.workflowId],
            "ConnectionId": that.connectionId
        };

        var request = $.ajax({
            url: "/store/remove",
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
            bootbox.alert({ message: "Failed to delete workflow: " + errorThrown, size: 'small' });
        });
        request.always(function (xhr, textStatus) {
            $('#spinner-single').hide();
            $('#btn-suspend-single').prop('disabled', false);
            $('#btn-delete-single').prop('disabled', false);
        });
    };

    this.toggleWorkflowSuspension = function () {
        //debugger;
        var suspend = ($('#btn-suspend-single').html() == 'Suspend');
        $('#spinner-single').show();
        $('#btn-suspend-single').prop('disabled', true);
        $('#btn-delete-single').prop('disabled', true);

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
            $('#spinner-single').hide();
            $('#btn-suspend-single').prop('disabled', false);
            $('#btn-delete-single').prop('disabled', false);
        });
    };

};

var StoreViewWorkflowDefinitionViewModel = function () {

    var that = this;

    this.workflowQualifiedName = null;
    this.connectionId = null;
    this.closeDialogCallback = null;

    this.showDialog = function (workflowQualifiedName) {

        that.workflowQualifiedName = workflowQualifiedName;
        that.connectionId = $('#pnl-workflows').data().modelId;

        $('#dlg-workflow-definition').modal('show');

        var spinnerElement = $('#definition-spinner-single').show();
        var errorElement = $('#workflow-definition-msg-error').hide();
        var headerElement = $('#workflow-definition-header-id').html(that.workflowQualifiedName);
        var graphElement = $('#workflow-definition-graph').empty().append('Loading graph data....');

        var request = $.ajax({
            url: '/store/definition?id=' + that.connectionId + '&qname=' + that.workflowQualifiedName,
            method: "GET",
            dataType: 'json'
        });

        request.done(function (response) {

            //debugger;
            var viz = new Viz();
            var options = {
                engine: 'dot',
                interaction: { hover: true },
                nodes: { color: 'red', font: { size: 14, color: "#000" } }
            };

            viz.renderSVGElement(response.graph, options)
                .then(function (element) {
                    graphElement.empty().append(element);
                    $(element).width('100%');
                })
                .catch(error => {
                    graphElement.empty().append('An error occurred:' + error);
                });

        });

        request.fail(function (xhr, textStatus, errorThrown) {
            //debugger;
            if (xhr.status == 404 && xhr.responseJSON != null && xhr.responseJSON.message != null) {
                errorElement.html(xhr.responseJSON.message);
                headerElement.html('Workflow Definition Not Found');
            }
            else {
                errorElement.html('An error has occurred: ' + xhr.errorThrown);
            }
            errorElement.show().removeClass('hidden');
        });
        request.always(function (xhr, textStatus) {
            spinnerElement.hide();
        });
    };
};

$(document).ready(function()
{
    new StoreView().init();
});
