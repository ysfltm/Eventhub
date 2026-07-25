namespace EventHub.API.Services;

public interface IQRCodeService
{
    byte[] GenerateQrCodePng(string payload);
}