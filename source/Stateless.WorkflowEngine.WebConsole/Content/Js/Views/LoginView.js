var LoginView = function () {

    var that = this;

    this.selectorErrorMessage = '#msg-error';
    this.selectorLoginButton = '#btn-login';
    this.selectorUserName = '#userName';
    this.selectorPassword = '#password';
    this.selectorSpinner = '#spinner';

    this.init = function () {
        $(this.selectorLoginButton).on('click', that.submitForm);
        $(that.selectorPassword).on('keypress', function (e) {
            if (e.which == 13) {
                that.submitForm();
                return false; 
            }
        });
        $(this.selectorUserName).focus();
    };

    this.submitForm = function () {
        //debugger;
        var frm = $('#frm-login');
        $(that.selectorErrorMessage).addClass('hidden');
        $(that.selectorLoginButton).prop('disabled', true);
        $(that.selectorSpinner).removeClass("hide");
        var formData = {
            userName: $(that.selectorUserName).val(),
            password: $(that.selectorPassword).val(),
        };
        var request = $.ajax({
            url: frm.attr('action'),
            method: "POST",
            data: formData
        });

        request.always(function(xhr, textStatus, errorThrown) { 
            $(that.selectorLoginButton).prop('disabled', false);
            $(that.selectorSpinner).addClass("hide");
        });
        request.done(function (response) {
            if (response.success === false) {
                Utils.showError(that.selectorErrorMessage, 'Unable to sign in using the supplied email address and password');
            }
            else {
                window.location.assign($('#returnUrl').val());
            }
        });

        request.fail(function (xhr, textStatus) {
            try {
                Utils.showError(that.selectorErrorMessage, xhr.responseJSON.message);
            }
            catch(err) {
                Utils.showError(that.selectorErrorMessage, 'A fatal error occurred');
            }
        });
    };

}


$(document).ready(function()
{
    var lv = new LoginView();
    lv.init();
});
