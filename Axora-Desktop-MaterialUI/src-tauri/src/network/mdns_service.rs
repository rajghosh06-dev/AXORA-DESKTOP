/// network/mdns_service.rs — mDNS service advertisement for local Wi-Fi discovery
///
/// Advertises this Axora Desktop instance as `_axora._tcp.local`
/// so the Axora Mobile app on Android 16+ can discover it instantly
/// using the NSD (Network Service Discovery) API.

use mdns_sd::{ServiceDaemon, ServiceInfo};

pub async fn advertise_service(port: u16) -> Result<(), String> {
    let mdns = ServiceDaemon::new().map_err(|e| format!("mDNS daemon error: {}", e))?;

    // Hostname for this machine (use computer name for Android NSD readability)
    let hostname = hostname::get()
        .unwrap_or_else(|_| std::ffi::OsString::from("axora-pc"))
        .to_string_lossy()
        .to_string();
    let fqdn_hostname = format!("{}.local.", hostname);

    let service_info = ServiceInfo::new(
        "_axora._tcp.local.",                // Service type
        "Axora Desktop",                     // Instance name
        &fqdn_hostname,                      // Hostname
        "",                                  // IP (empty = auto-detect)
        port,
        None,                                // TXT records (optional metadata)
    )
    .map_err(|e| format!("mDNS service info error: {}", e))?;

    mdns.register(service_info)
        .map_err(|e| format!("mDNS register error: {}", e))?;

    println!(
        "[Axora mDNS] Advertised _axora._tcp.local on port {}",
        port
    );

    // Keep the daemon alive — it runs until the process exits
    // In a real implementation, we'd hold the ServiceDaemon in a global Arc
    // so it can be unregistered cleanly on shutdown.
    std::future::pending::<()>().await;
    Ok(())
}

